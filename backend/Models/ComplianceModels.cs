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
