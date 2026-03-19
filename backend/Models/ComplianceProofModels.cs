namespace BankReporting.Api.Models;

/// <summary>
/// 標準化合規證明模型（v1）
/// </summary>
public class ComplianceProof
{
    public string ProofId { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = "COMPLIANCE_PROOF_V1";
    public string SubjectType { get; set; } = "REPORT_DECLARATION";
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string ReportYear { get; set; } = string.Empty;
    public string? ReportMonth { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string DigestAlgorithm { get; set; } = "SHA256";
    public string Canonicalization { get; set; } = "JSON_CANONICAL_V1";
    public string DataDigest { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
    public BlockchainAnchor? Anchor { get; set; }
    public List<AuditTrailEntry> AuditTrail { get; set; } = new();
}

public class BlockchainAnchor
{
    public string Network { get; set; } = string.Empty;
    public string AdapterName { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string BlockId { get; set; } = string.Empty;
    public DateTimeOffset AnchoredAt { get; set; }
    public string AnchorHash { get; set; } = string.Empty;
}

public class AuditTrailEntry
{
    public string EventId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Actor { get; set; } = "system";
    public DateTimeOffset OccurredAt { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class ComplianceProofPayload
{
    public ComplianceProof Proof { get; set; } = new();
}

public class AuditTrailPayload
{
    public string CorrelationId { get; set; } = string.Empty;
    public List<AuditTrailEntry> Events { get; set; } = new();
}

public class BlockchainAnchorRequest
{
    public string Network { get; set; } = "simulated-mainnet";
    public string DataDigest { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}

public class BlockchainAnchorResult
{
    public string Network { get; set; } = string.Empty;
    public string AdapterName { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string BlockId { get; set; } = string.Empty;
    public DateTimeOffset AnchoredAt { get; set; }
    public string AnchorHash { get; set; } = string.Empty;
}
