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
    private readonly IComplianceProofPersistence _persistence;
    private readonly SemaphoreSlim _storeLock = new(1, 1);
    private ComplianceProofStoreSnapshot _store;

    public ComplianceProofService(
        IBlockchainAdapterService blockchainAdapterService,
        IComplianceProofPersistence persistence)
    {
        _blockchainAdapterService = blockchainAdapterService;
        _persistence = persistence;
        _store = _persistence.Load();
    }

    public async Task<ApiResponse<ComplianceProofPayload>> CreateProofAsync(CreateComplianceProofRequest request)
    {
        await _storeLock.WaitAsync();
        try
        {
            var bankCode = request.BankCode.Trim();
            var reportId = request.ReportId.Trim();
            var reportYear = request.ReportYear.Trim();
            var reportMonth = request.ReportMonth?.Trim();
            var requestId = request.RequestId.Trim();
            var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId)
                ? requestId
                : request.CorrelationId!.Trim();
            var idempotencyKey = BuildIdempotencyKey(request, bankCode, reportId, reportYear, reportMonth, requestId);

            if (_store.IdempotencyKeyToProofId.TryGetValue(idempotencyKey, out var existingProofId) &&
                _store.Proofs.TryGetValue(existingProofId, out var existingProof))
            {
                AppendAudit(existingProof.CorrelationId, "IDEMPOTENCY_HIT", new Dictionary<string, string>
                {
                    ["proofId"] = existingProof.ProofId,
                    ["idempotencyKey"] = idempotencyKey
                });

                existingProof.AuditTrail = GetOrderedAuditTrail(existingProof.CorrelationId);
                SaveStore();

                return new ApiResponse<ComplianceProofPayload>
                {
                    Code = "0000",
                    Msg = "重複請求，返回既有合規證明",
                    Payload = new ComplianceProofPayload { Proof = existingProof }
                };
            }

            var canonicalJson = BuildCanonicalJson(new
            {
                schemaVersion = "COMPLIANCE_PROOF_V1",
                subjectType = "REPORT_DECLARATION",
                bankCode,
                reportId,
                reportYear,
                reportMonth,
                requestId,
                correlationId,
                reportPayload = request.ReportPayload
            });

            var dataDigest = ComputeSha256(canonicalJson);
            var generatedAt = DateTimeOffset.UtcNow;
            var proofId = $"PRF-{generatedAt:yyyyMMddHHmmss}-{Guid.NewGuid():N}";

            AppendAudit(correlationId, "PROOF_STANDARDIZED", new Dictionary<string, string>
            {
                ["proofId"] = proofId,
                ["digest"] = dataDigest,
                ["idempotencyKey"] = idempotencyKey
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
                BankCode = bankCode,
                ReportId = reportId,
                ReportYear = reportYear,
                ReportMonth = reportMonth,
                RequestId = requestId,
                CorrelationId = correlationId,
                DataDigest = dataDigest,
                Statement = $"Report {reportId} for bank {bankCode} is anchored with digest {dataDigest}.",
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
                AuditTrail = GetOrderedAuditTrail(correlationId)
            };

            _store.Proofs[proofId] = proof;
            _store.TransactionToProofId[anchor.TransactionId] = proofId;
            _store.IdempotencyKeyToProofId[idempotencyKey] = proofId;
            SaveStore();

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
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<ApiResponse<ComplianceProofPayload>> GetProofByIdAsync(string proofId)
    {
        await _storeLock.WaitAsync();
        try
        {
            if (_store.Proofs.TryGetValue(proofId, out var proof))
            {
                proof.AuditTrail = GetOrderedAuditTrail(proof.CorrelationId);
                return new ApiResponse<ComplianceProofPayload>
                {
                    Code = "0000",
                    Msg = "查詢成功",
                    Payload = new ComplianceProofPayload { Proof = proof }
                };
            }

            return new ApiResponse<ComplianceProofPayload>
            {
                Code = "4040",
                Msg = "查無證明"
            };
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<ApiResponse<ComplianceProofPayload>> GetProofByTransactionIdAsync(string transactionId)
    {
        await _storeLock.WaitAsync();
        try
        {
            if (_store.TransactionToProofId.TryGetValue(transactionId, out var proofId) &&
                _store.Proofs.TryGetValue(proofId, out var proof))
            {
                proof.AuditTrail = GetOrderedAuditTrail(proof.CorrelationId);
                return new ApiResponse<ComplianceProofPayload>
                {
                    Code = "0000",
                    Msg = "查詢成功",
                    Payload = new ComplianceProofPayload { Proof = proof }
                };
            }

            return new ApiResponse<ComplianceProofPayload>
            {
                Code = "4040",
                Msg = "查無交易對應證明"
            };
        }
        finally
        {
            _storeLock.Release();
        }
    }

    public async Task<ApiResponse<AuditTrailPayload>> GetAuditTrailByCorrelationIdAsync(string correlationId)
    {
        await _storeLock.WaitAsync();
        try
        {
            if (_store.AuditTrailByCorrelationId.TryGetValue(correlationId, out _))
            {
                return new ApiResponse<AuditTrailPayload>
                {
                    Code = "0000",
                    Msg = "查詢成功",
                    Payload = new AuditTrailPayload
                    {
                        CorrelationId = correlationId,
                        Events = GetOrderedAuditTrail(correlationId)
                    }
                };
            }

            return new ApiResponse<AuditTrailPayload>
            {
                Code = "4040",
                Msg = "查無稽核軌跡"
            };
        }
        finally
        {
            _storeLock.Release();
        }
    }

    private static string BuildIdempotencyKey(
        CreateComplianceProofRequest request,
        string bankCode,
        string reportId,
        string reportYear,
        string? reportMonth,
        string requestId)
    {
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return request.IdempotencyKey!.Trim();
        }

        return $"{bankCode}:{reportId}:{reportYear}:{reportMonth}:{requestId}";
    }

    private void AppendAudit(string correlationId, string eventType, Dictionary<string, string> metadata)
    {
        if (!_store.AuditTrailByCorrelationId.TryGetValue(correlationId, out var events))
        {
            events = new List<AuditTrailEntry>();
            _store.AuditTrailByCorrelationId[correlationId] = events;
        }

        events.Add(new AuditTrailEntry
        {
            EventId = Guid.NewGuid().ToString("N"),
            CorrelationId = correlationId,
            EventType = eventType,
            OccurredAt = DateTimeOffset.UtcNow,
            Metadata = metadata
        });
    }

    private List<AuditTrailEntry> GetOrderedAuditTrail(string correlationId)
    {
        if (!_store.AuditTrailByCorrelationId.TryGetValue(correlationId, out var events))
        {
            return new List<AuditTrailEntry>();
        }

        return events.OrderBy(x => x.OccurredAt).ToList();
    }

    private static string BuildCanonicalJson(object value)
    {
        var element = value is JsonElement jsonElement
            ? jsonElement
            : JsonSerializer.SerializeToElement(value);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = false
        }))
        {
            WriteCanonicalElement(writer, element);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var int64Value))
                {
                    writer.WriteNumberValue(int64Value);
                }
                else if (element.TryGetDecimal(out var decimalValue))
                {
                    writer.WriteNumberValue(decimalValue);
                }
                else if (element.TryGetDouble(out var doubleValue))
                {
                    writer.WriteNumberValue(doubleValue);
                }
                else
                {
                    writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                }
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                writer.WriteBooleanValue(element.GetBoolean());
                break;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                writer.WriteNullValue();
                break;
        }
    }

    private void SaveStore() => _persistence.Save(_store);

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
