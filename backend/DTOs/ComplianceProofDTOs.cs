namespace BankReporting.Api.DTOs;

/// <summary>
/// 建立合規證明請求
/// </summary>
public class CreateComplianceProofRequest
{
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string ReportYear { get; set; } = string.Empty;
    public string? ReportMonth { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public string? IdempotencyKey { get; set; }
    public object? ReportPayload { get; set; }
}
