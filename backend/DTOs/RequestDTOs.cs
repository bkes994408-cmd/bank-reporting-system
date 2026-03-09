namespace BankReporting.Api.DTOs;

/// <summary>
/// Excel轉JSON請求
/// </summary>
public class ExcelParsingRequest
{
    public string ReportId { get; set; } = string.Empty;
    public IFormFile? UploadFile { get; set; }
}

/// <summary>
/// Excel + 聯絡人轉JSON請求
/// </summary>
public class ExcelWithContactRequest
{
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string ReportYear { get; set; } = string.Empty;
    public string ReportMonth { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string ContractorTel { get; set; } = string.Empty;
    public string ContractorEmail { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string ManagerTel { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public IFormFile? UploadFile { get; set; }
}

/// <summary>
/// 申報上傳請求
/// </summary>
public class DeclareRequest
{
    public string RequestId { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string ReportYear { get; set; } = string.Empty;
    public string ReportMonth { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public string ContractorTel { get; set; } = string.Empty;
    public string ContractorEmail { get; set; } = string.Empty;
    public string ManagerName { get; set; } = string.Empty;
    public string ManagerTel { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;
    public object? Report { get; set; }

    // 若代理端啟用簽章/JWE，可使用以下欄位透傳
    public bool UseSignature { get; set; }
    public string? Signature { get; set; }
    public bool UseJwe { get; set; }
    public string? JwePayload { get; set; }
}

/// <summary>
/// 查詢申報結果請求
/// </summary>
public class DeclareResultRequest
{
    public string? RequestId { get; set; }
    public string? TransactionId { get; set; }
}

/// <summary>
/// 查詢當月報表請求
/// </summary>
public class MonthlyReportsRequest
{
    public string BankCode { get; set; } = string.Empty;
    public string ApplyYear { get; set; } = string.Empty;
    public string? ApplyMonth { get; set; }
}

/// <summary>
/// 查詢報表歷程請求
/// </summary>
public class ReportHistoriesRequest
{
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string? Type { get; set; }
}

/// <summary>
/// 匯入金鑰請求
/// </summary>
public class ImportKeysRequest
{
    public string KeyA { get; set; } = string.Empty;
    public string KeyB { get; set; } = string.Empty;
}

/// <summary>
/// 更新Token請求
/// </summary>
public class UpdateTokenRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// 公告查詢請求
/// </summary>
public class NewsRequest
{
    public int? PageNumber { get; set; }
    public int? PageSize { get; set; }
    public string? Keyword { get; set; }
    public string? Sort { get; set; }
}

/// <summary>
/// 下載附件請求
/// </summary>
public class AttachmentDownloadRequest
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

/// <summary>
/// AD 網域登入請求
/// </summary>
public class AdLoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// 新增後台使用者
/// </summary>
public class AdminCreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// 更新後台使用者角色
/// </summary>
public class AdminUpdateUserRolesRequest
{
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// 後台更新帳號權限請求
/// </summary>
public class UpdateAccountRolesRequest
{
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// 第三方系統資料同步請求
/// </summary>
public class ThirdPartySyncRequest
{
    public string SystemName { get; set; } = string.Empty;
    public string EventType { get; set; } = "report.declaration";
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RequestId { get; set; }
    public string? TransactionId { get; set; }
    public object? Data { get; set; }
}
