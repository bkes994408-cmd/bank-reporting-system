namespace BankReporting.Api.Models;

public class ThirdPartySystemConfig
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string SyncPath { get; set; } = "/api/reporting/sync";
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; }
    public int TimeoutSeconds { get; set; } = 10;
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
}

public class ThirdPartySystemsPayload
{
    public List<string> Systems { get; set; } = new();
}
