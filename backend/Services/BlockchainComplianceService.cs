using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IBlockchainComplianceService
{
    BlockchainAuditAnchorRecord CommitAuditAnchor(BlockchainAuditAnchorCommitRequest request);
    BlockchainAuditAnchorQueryPayload QueryAuditAnchors(BlockchainAuditAnchorQueryRequest request);
    BlockchainDataSharingSimulationResult SimulateDataSharing(BlockchainDataSharingSimulationRequest request);
}

public class BlockchainComplianceService : IBlockchainComplianceService
{
    private readonly ConcurrentDictionary<string, BlockchainAuditAnchorRecord> _anchors = new(StringComparer.OrdinalIgnoreCase);

    public BlockchainAuditAnchorRecord CommitAuditAnchor(BlockchainAuditAnchorCommitRequest request)
    {
        var now = DateTime.UtcNow;
        var anchorId = $"anchor-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..36];

        var normalizedTrailIds = (request.AuditTrailIds ?? new List<string>())
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var network = string.IsNullOrWhiteSpace(request.Network) ? "sandbox-ledger" : request.Network.Trim();
        var anchorType = string.IsNullOrWhiteSpace(request.AnchorType) ? "audit_trail" : request.AnchorType.Trim();
        var summary = string.IsNullOrWhiteSpace(request.Summary) ? "compliance checkpoint" : request.Summary.Trim();

        var previous = _anchors.Values
            .Where(x => string.Equals(x.Network, network, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        var payloadHash = Sha256Hex(string.IsNullOrWhiteSpace(request.PayloadHash)
            ? BuildSourceDigest(anchorType, normalizedTrailIds, summary, request.Metadata)
            : request.PayloadHash.Trim().ToLowerInvariant());

        var previousHash = previous?.AnchorHash;
        var anchorHash = Sha256Hex($"{payloadHash}|{previousHash ?? "GENESIS"}|{now:O}|{network}");

        var record = new BlockchainAuditAnchorRecord
        {
            AnchorId = anchorId,
            AnchorType = anchorType,
            Network = network,
            Summary = summary,
            PayloadHash = payloadHash,
            AnchorHash = anchorHash,
            PreviousAnchorHash = previousHash,
            SuggestedVerification = $"重新計算 SHA-256(payloadHash|previousHash|timestamp|network) 並與 {anchorHash[..16]}... 比對",
            AuditTrailIds = normalizedTrailIds,
            Metadata = new Dictionary<string, string>(request.Metadata ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase),
            CreatedAtUtc = now
        };

        _anchors[anchorId] = record;
        return record;
    }

    public BlockchainAuditAnchorQueryPayload QueryAuditAnchors(BlockchainAuditAnchorQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _anchors.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.Network))
        {
            var network = request.Network.Trim();
            query = query.Where(x => string.Equals(x.Network, network, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.AnchorType))
        {
            var anchorType = request.AnchorType.Trim();
            query = query.Where(x => string.Equals(x.AnchorType, anchorType, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FromCreatedAtUtc.HasValue)
        {
            var fromUtc = request.FromCreatedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.CreatedAtUtc >= fromUtc);
        }

        if (request.ToCreatedAtUtc.HasValue)
        {
            var toUtc = request.ToCreatedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.CreatedAtUtc <= toUtc);
        }

        var ordered = query.OrderByDescending(x => x.CreatedAtUtc).ToList();
        return new BlockchainAuditAnchorQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Records = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    public BlockchainDataSharingSimulationResult SimulateDataSharing(BlockchainDataSharingSimulationRequest request)
    {
        var now = DateTime.UtcNow;
        var fields = (request.Fields ?? new List<string>())
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var policyViolations = new List<string>();
        if (fields.Any(x => x.Contains("id", StringComparison.OrdinalIgnoreCase) || x.Contains("name", StringComparison.OrdinalIgnoreCase)))
        {
            policyViolations.Add("欄位包含可能識別個資，建議使用 tokenization / masking");
        }

        if (string.Equals(request.Purpose?.Trim(), "cross-bank-raw-share", StringComparison.OrdinalIgnoreCase))
        {
            policyViolations.Add("跨銀行原始資料共享風險高，建議改用 proof-only 方案");
        }

        var recommendedMode = policyViolations.Count == 0 ? "proof-with-hash-pointer" : "zk-proof-or-aggregated-metrics";
        var packageId = $"share-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];

        return new BlockchainDataSharingSimulationResult
        {
            PackageId = packageId,
            GeneratedAtUtc = now,
            Participants = new List<string>
            {
                request.SourceInstitution,
                request.TargetInstitution,
                request.Regulator ?? "n/a"
            }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Fields = fields,
            RecommendedMode = recommendedMode,
            PolicyViolations = policyViolations,
            SuggestedNextActions = policyViolations.Count == 0
                ? new List<string>
                {
                    "建立鏈下加密資料包，鏈上僅保留 hash pointer",
                    "於共享協議中註記資料用途與保存期限",
                    "導入定期驗證作業，確認 hash 一致性"
                }
                : new List<string>
                {
                    "調整欄位為脫敏或統計值後再共享",
                    "補充法遵審批紀錄與用途限制",
                    "評估採用零知識證明或多方安全計算"
                }
        };
    }

    private static string BuildSourceDigest(string anchorType, List<string> trailIds, string summary, Dictionary<string, string>? metadata)
    {
        var metadataText = metadata is null
            ? string.Empty
            : string.Join(";", metadata.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).Select(x => $"{x.Key.Trim()}={x.Value?.Trim()}"));

        return Sha256Hex($"{anchorType}|{summary}|{string.Join(',', trailIds)}|{metadataText}");
    }

    private static string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
