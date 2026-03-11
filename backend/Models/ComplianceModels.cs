namespace BankReporting.Api.Models;

public class AuditTrailRecord
{
    public DateTime TimestampUtc { get; set; }
    public string User { get; set; } = "anonymous";
    public string? Role { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public bool IsSensitiveOperation { get; set; }
    public string RiskLevel { get; set; } = "low";
}

public class ComplianceAuditSummary
{
    public int TotalRequests { get; set; }
    public int FailedRequests { get; set; }
    public int SensitiveOperations { get; set; }
    public int HighRiskOperations { get; set; }
    public int UniqueUsers { get; set; }
}

public class ComplianceAuditReportRecord
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public ComplianceAuditSummary Summary { get; set; } = new();
    public List<string> TopSensitiveEndpoints { get; set; } = new();
}

public class ComplianceAuditReportsPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ComplianceAuditReportRecord> Reports { get; set; } = new();
}

public class AuditTrailQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AuditTrailRecord> Records { get; set; } = new();
}

public class RegulationDocumentSnapshot
{
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public string? Url { get; set; }
    public List<string> Clauses { get; set; } = new();
}

public class RegulationChangeItem
{
    public string ChangeType { get; set; } = "updated"; // added|removed|updated
    public string ClauseKey { get; set; } = string.Empty;
    public string? PreviousText { get; set; }
    public string? CurrentText { get; set; }
}

public class RegulationImpactArea
{
    public string Domain { get; set; } = string.Empty;
    public string Severity { get; set; } = "low"; // low|medium|high
    public string Reason { get; set; } = string.Empty;
}

public class RegulationImpactAnalysisRecord
{
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime BaselinePublishedAtUtc { get; set; }
    public DateTime CurrentPublishedAtUtc { get; set; }
    public List<RegulationChangeItem> Changes { get; set; } = new();
    public List<RegulationImpactArea> ImpactAreas { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

public class RegulationImpactQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<RegulationImpactAnalysisRecord> Records { get; set; } = new();
}

public class ExternalComplianceProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string FetchPath { get; set; } = "/api/v1/risk-list";
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
}

public class ExternalRiskRecord
{
    public string RecordId { get; set; } = Guid.NewGuid().ToString("N");
    public string ProviderName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = "sanctions";
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string RiskLevel { get; set; } = "medium";
    public List<string> Tags { get; set; } = new();
    public DateTime ImportedAtUtc { get; set; }
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public Dictionary<string, string> Raw { get; set; } = new();
}

public class ExternalRiskDataSyncResult
{
    public string ProviderName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = string.Empty;
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}

public class ExternalRiskMatchItem
{
    public string RecordId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string RiskLevel { get; set; } = "medium";
    public double Score { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class ExternalRiskScreeningResult
{
    public string CustomerName { get; set; } = string.Empty;
    public string? Country { get; set; }
    public int TotalMatches { get; set; }
    public string SuggestedDecision { get; set; } = "clear";
    public List<ExternalRiskMatchItem> Matches { get; set; } = new();
}
