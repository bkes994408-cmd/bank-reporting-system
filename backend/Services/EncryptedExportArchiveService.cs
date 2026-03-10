using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using Microsoft.Extensions.Configuration;

namespace BankReporting.Api.Services;

public interface IEncryptedExportArchiveService
{
    Task<EncryptedArchiveRecord> ArchiveReportHistoriesAsync(ReportHistoriesRequest request, CancellationToken cancellationToken = default);
    Task<EncryptedArchiveRecord> ArchiveDeclareResultAsync(DeclareResultRequest request, CancellationToken cancellationToken = default);
    EncryptedArchiveQueryPayload Query(EncryptedArchiveQueryRequest request);
}

public class EncryptedExportArchiveService : IEncryptedExportArchiveService
{
    private readonly IAgentService _agentService;
    private readonly byte[] _key;
    private readonly object _lock = new();
    private readonly List<EncryptedArchiveRecord> _records = new();

    public EncryptedExportArchiveService(IAgentService agentService, IConfiguration configuration)
    {
        _agentService = agentService;
        _key = ResolveKey(configuration);
    }

    public async Task<EncryptedArchiveRecord> ArchiveReportHistoriesAsync(ReportHistoriesRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _agentService.GetReportHistoriesAsync(request);
        if (response.Code != "0000")
        {
            throw new InvalidOperationException($"archive failed: {response.Code} {response.Msg}");
        }

        var payload = new
        {
            type = "report-histories",
            request.BankCode,
            request.ReportId,
            request.Year,
            request.Type,
            reports = response.Payload?.Reports ?? new List<ReportHistory>()
        };

        var record = EncryptPayload("report-histories", request.BankCode, request.ReportId, request.Year, null, null, payload);
        lock (_lock)
        {
            _records.Add(record);
        }

        return record;
    }

    public async Task<EncryptedArchiveRecord> ArchiveDeclareResultAsync(DeclareResultRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _agentService.GetDeclareResultAsync(request);
        if (response.Code != "0000")
        {
            throw new InvalidOperationException($"archive failed: {response.Code} {response.Msg}");
        }

        var payload = new
        {
            type = "declare-result",
            request.RequestId,
            request.TransactionId,
            result = response.Payload
        };

        var reportId = response.Payload?.ReportId ?? string.Empty;
        var bankCode = response.Payload?.BankCode ?? string.Empty;
        var year = response.Payload?.Year;

        var record = EncryptPayload(
            "declare-result",
            bankCode,
            reportId,
            year,
            request.RequestId,
            request.TransactionId,
            payload);

        lock (_lock)
        {
            _records.Add(record);
        }

        return record;
    }

    public EncryptedArchiveQueryPayload Query(EncryptedArchiveQueryRequest request)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        List<EncryptedArchiveRecord> snapshot;
        lock (_lock)
        {
            snapshot = _records.ToList();
        }

        var query = snapshot.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(x => string.Equals(x.Category, request.Category, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(request.BankCode))
        {
            query = query.Where(x => x.BankCode == request.BankCode);
        }
        if (!string.IsNullOrWhiteSpace(request.ReportId))
        {
            query = query.Where(x => x.ReportId == request.ReportId);
        }
        if (!string.IsNullOrWhiteSpace(request.RequestId))
        {
            query = query.Where(x => x.RequestIdMasked == Mask(request.RequestId));
        }
        if (!string.IsNullOrWhiteSpace(request.TransactionId))
        {
            query = query.Where(x => x.TransactionIdMasked == Mask(request.TransactionId));
        }
        if (request.StartDateUtc.HasValue)
        {
            query = query.Where(x => x.ArchivedAtUtc >= request.StartDateUtc.Value);
        }
        if (request.EndDateUtc.HasValue)
        {
            query = query.Where(x => x.ArchivedAtUtc <= request.EndDateUtc.Value);
        }

        query = query.OrderByDescending(x => x.ArchivedAtUtc);
        var total = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new EncryptedArchiveQueryPayload
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Records = items
        };
    }

    private EncryptedArchiveRecord EncryptPayload(
        string category,
        string bankCode,
        string reportId,
        string? year,
        string? requestId,
        string? transactionId,
        object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var plainBytes = Encoding.UTF8.GetBytes(json);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(_key, 16))
        {
            aes.Encrypt(nonce, plainBytes, cipher, tag);
        }

        var hash = SHA256.HashData(plainBytes);

        return new EncryptedArchiveRecord
        {
            ArchiveId = Guid.NewGuid().ToString("N"),
            Category = category,
            BankCode = bankCode,
            ReportId = reportId,
            Year = year,
            RequestIdMasked = Mask(requestId),
            TransactionIdMasked = Mask(transactionId),
            CipherTextBase64 = Convert.ToBase64String(cipher),
            NonceBase64 = Convert.ToBase64String(nonce),
            TagBase64 = Convert.ToBase64String(tag),
            DataSha256Hex = Convert.ToHexString(hash),
            ArchivedAtUtc = DateTime.UtcNow
        };
    }

    private static byte[] ResolveKey(IConfiguration configuration)
    {
        var keyBase64 = configuration["EncryptionArchive:KeyBase64"];
        if (!string.IsNullOrWhiteSpace(keyBase64))
        {
            try
            {
                var key = Convert.FromBase64String(keyBase64);
                if (key.Length == 32)
                {
                    return key;
                }
            }
            catch
            {
                // ignored
            }
        }

        var passphrase = configuration["EncryptionArchive:Passphrase"];
        if (!string.IsNullOrWhiteSpace(passphrase))
        {
            return SHA256.HashData(Encoding.UTF8.GetBytes(passphrase));
        }

        return RandomNumberGenerator.GetBytes(32);
    }

    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 4)
        {
            return new string('*', trimmed.Length);
        }

        return $"{trimmed[..2]}***{trimmed[^2..]}";
    }
}
