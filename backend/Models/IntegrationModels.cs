namespace BankReporting.Api.Models;

public class ThirdPartySystemConfig
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string SyncPath { get; set; } = "/api/reporting/sync";
    public string? CompensationPath { get; set; }
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 2;
    public int RetryDelayMilliseconds { get; set; } = 200;
    public List<int> RetryableStatusCodes { get; set; } = new() { 408, 429, 500, 502, 503, 504 };
}

public class ThirdPartySyncPayload
{
    public string SystemName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? TransactionId { get; set; }
    public object? Data { get; set; }
    public DateTimeOffset SyncedAtUtc { get; set; }
}

public class ThirdPartySyncResult
{
    public string SystemName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string? DeadLetterId { get; set; }
}

public class ThirdPartySystemsPayload
{
    public List<string> Systems { get; set; } = new();
}

public class ThirdPartyDeadLetterRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ThirdPartySyncPayload Payload { get; set; } = new();
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int LastStatusCode { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset FirstFailedAtUtc { get; set; }
    public DateTimeOffset LastFailedAtUtc { get; set; }
    public bool CompensationExecuted { get; set; }
    public string? CompensationResult { get; set; }
}

public class ThirdPartyDeadLetterPayload
{
    public List<ThirdPartyDeadLetterRecord> Items { get; set; } = new();
}

public class RegulatoryAuditReportSyncResult
{
    public string BankCode { get; set; } = string.Empty;
    public string PlatformSystemName { get; set; } = string.Empty;
    public ComplianceAuditReportRecord AuditReport { get; set; } = new();
    public ThirdPartySyncResult SyncResult { get; set; } = new();
    public bool Synced { get; set; }
}
