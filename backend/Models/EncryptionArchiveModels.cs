namespace BankReporting.Api.Models;

public class EncryptedArchiveRecord
{
    public string ArchiveId { get; set; } = Guid.NewGuid().ToString("N");
    public string Category { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string? Year { get; set; }
    public string? RequestIdMasked { get; set; }
    public string? TransactionIdMasked { get; set; }
    public string CipherTextBase64 { get; set; } = string.Empty;
    public string NonceBase64 { get; set; } = string.Empty;
    public string TagBase64 { get; set; } = string.Empty;
    public string DataSha256Hex { get; set; } = string.Empty;
    public DateTime ArchivedAtUtc { get; set; }
}

public class EncryptedArchiveQueryRequest
{
    public string? Category { get; set; }
    public string? BankCode { get; set; }
    public string? ReportId { get; set; }
    public string? RequestId { get; set; }
    public string? TransactionId { get; set; }
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class EncryptedArchiveQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<EncryptedArchiveRecord> Records { get; set; } = new();
}
