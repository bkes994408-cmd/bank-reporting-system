using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IComplianceProofService
{
    Task<ApiResponse<ComplianceProofPayload>> CreateProofAsync(CreateComplianceProofRequest request);
    Task<ApiResponse<ComplianceProofPayload>> GetProofByIdAsync(string proofId);
    Task<ApiResponse<ComplianceProofPayload>> GetProofByTransactionIdAsync(string transactionId);
    Task<ApiResponse<AuditTrailPayload>> GetAuditTrailByCorrelationIdAsync(string correlationId);
}

public class ComplianceProofService : IComplianceProofService
{
    private readonly IBlockchainAdapterService _blockchainAdapterService;
    private readonly ConcurrentDictionary<string, ComplianceProof> _proofStore = new();
    private readonly ConcurrentDictionary<string, string> _txToProofId = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<AuditTrailEntry>> _auditStore = new();

    public ComplianceProofService(IBlockchainAdapterService blockchainAdapterService)
    {
        _blockchainAdapterService = blockchainAdapterService;
    }

    public async Task<ApiResponse<ComplianceProofPayload>> CreateProofAsync(CreateComplianceProofRequest request)
    {
        try
        {
            var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? request.RequestId
                : request.CorrelationId!.Trim();

            var normalizedPayload = new
            {
                schemaVersion = "COMPLIANCE_PROOF_V1",
                subjectType = "REPORT_DECLARATION",
                bankCode = request.BankCode.Trim(),
                reportId = request.ReportId.Trim(),
                reportYear = request.ReportYear.Trim(),
                reportMonth = request.ReportMonth?.Trim(),
                requestId = request.RequestId.Trim(),
                correlationId,
                reportPayload = request.ReportPayload
            };

            var canonicalJson = JsonSerializer.Serialize(normalizedPayload);
            var dataDigest = ComputeSha256(canonicalJson);
            var generatedAt = DateTimeOffset.UtcNow;
            var proofId = $"PRF-{generatedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

            AppendAudit(correlationId, "PROOF_STANDARDIZED", new Dictionary<string, string>
            {
                ["proofId"] = proofId,
                ["digest"] = dataDigest
            });

            var anchor = await _blockchainAdapterService.AnchorAsync(new BlockchainAnchorRequest
            {
                Network = "simulated-mainnet",
                DataDigest = dataDigest,
                CorrelationId = correlationId
            });

            AppendAudit(correlationId, "BLOCKCHAIN_ANCHORED", new Dictionary<string, string>
            {
                ["proofId"] = proofId,
                ["transactionId"] = anchor.TransactionId,
                ["blockId"] = anchor.BlockId
            });

            var proof = new ComplianceProof
            {
                ProofId = proofId,
                BankCode = request.BankCode.Trim(),
                ReportId = request.ReportId.Trim(),
                ReportYear = request.ReportYear.Trim(),
                ReportMonth = request.ReportMonth?.Trim(),
                RequestId = request.RequestId.Trim(),
                CorrelationId = correlationId,
                DataDigest = dataDigest,
                Statement = $"Report {request.ReportId.Trim()} for bank {request.BankCode.Trim()} is anchored with digest {dataDigest}.",
                GeneratedAt = generatedAt,
                Anchor = new BlockchainAnchor
                {
                    Network = anchor.Network,
                    AdapterName = anchor.AdapterName,
                    TransactionId = anchor.TransactionId,
                    BlockId = anchor.BlockId,
                    AnchoredAt = anchor.AnchoredAt,
                    AnchorHash = anchor.AnchorHash
                },
                AuditTrail = _auditStore.TryGetValue(correlationId, out var events)
                    ? events.OrderBy(x => x.OccurredAt).ToList()
                    : new List<AuditTrailEntry>()
            };

            _proofStore[proofId] = proof;
            _txToProofId[anchor.TransactionId] = proofId;

            return new ApiResponse<ComplianceProofPayload>
            {
                Code = "0000",
                Msg = "合規證明建立成功",
                Payload = new ComplianceProofPayload { Proof = proof }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ComplianceProofPayload> { Code = "5000", Msg = ex.Message };
        }
    }

    public Task<ApiResponse<ComplianceProofPayload>> GetProofByIdAsync(string proofId)
    {
        if (_proofStore.TryGetValue(proofId, out var proof))
        {
            return Task.FromResult(new ApiResponse<ComplianceProofPayload>
            {
                Code = "0000",
                Msg = "查詢成功",
                Payload = new ComplianceProofPayload { Proof = proof }
            });
        }

        return Task.FromResult(new ApiResponse<ComplianceProofPayload>
        {
            Code = "4040",
            Msg = "查無證明"
        });
    }

    public Task<ApiResponse<ComplianceProofPayload>> GetProofByTransactionIdAsync(string transactionId)
    {
        if (_txToProofId.TryGetValue(transactionId, out var proofId))
        {
            return GetProofByIdAsync(proofId);
        }

        return Task.FromResult(new ApiResponse<ComplianceProofPayload>
        {
            Code = "4040",
            Msg = "查無交易對應證明"
        });
    }

    public Task<ApiResponse<AuditTrailPayload>> GetAuditTrailByCorrelationIdAsync(string correlationId)
    {
        if (_auditStore.TryGetValue(correlationId, out var events))
        {
            return Task.FromResult(new ApiResponse<AuditTrailPayload>
            {
                Code = "0000",
                Msg = "查詢成功",
                Payload = new AuditTrailPayload
                {
                    CorrelationId = correlationId,
                    Events = events.OrderBy(x => x.OccurredAt).ToList()
                }
            });
        }

        return Task.FromResult(new ApiResponse<AuditTrailPayload>
        {
            Code = "4040",
            Msg = "查無稽核軌跡"
        });
    }

    private void AppendAudit(string correlationId, string eventType, Dictionary<string, string> metadata)
    {
        var bag = _auditStore.GetOrAdd(correlationId, _ => new ConcurrentBag<AuditTrailEntry>());
        bag.Add(new AuditTrailEntry
        {
            EventId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            EventType = eventType,
            OccurredAt = DateTimeOffset.UtcNow,
            Metadata = metadata
        });
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
