namespace BankReporting.Api.Models;

/// <summary>
/// 標準API回應
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// 狀態碼
    /// </summary>
    public string Code { get; set; } = "0000";

    /// <summary>
    /// 訊息
    /// </summary>
    public string Msg { get; set; } = string.Empty;

    /// <summary>
    /// 資料載體
    /// </summary>
    public T? Payload { get; set; }
}

/// <summary>
/// 報表目錄項目
/// </summary>
public class ReportCatalogItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 報表目錄查詢結果
/// </summary>
public class ReportCatalogPayload
{
    public List<ReportCatalogItem> Items { get; set; } = new();
}

/// <summary>
/// 報表類型
/// </summary>
public static class ReportTypes
{
    public static readonly List<ReportCatalogItem> DefaultCatalog = new()
    {
        new() { Id = "AI302", Name = "資產負債表" },
        new() { Id = "AI330", Name = "授信擔保品別分析表" },
        new() { Id = "AI335", Name = "大額授信資料表" },
        new() { Id = "AI341", Name = "逾期放款統計表" },
        new() { Id = "AI345", Name = "逾期放款資料表" },
        new() { Id = "AI346", Name = "逾期放款結構分析表" },
        new() { Id = "AI370", Name = "聯合授信個案資料表" },
        new() { Id = "AI372", Name = "聯合授信額度資料表" },
        new() { Id = "AI395", Name = "不動產放款資料表" },
        new() { Id = "AI397", Name = "購屋貸款資料表" },
        new() { Id = "AI501", Name = "存放款利率表" },
        new() { Id = "AI505", Name = "存款結構分析表" },
        new() { Id = "AI515", Name = "放款結構分析表" },
        new() { Id = "AI520", Name = "利率敏感度缺口表" },
        new() { Id = "AI555", Name = "消費性放款資料表" },
        new() { Id = "AI560", Name = "信用卡業務資料表" },
        new() { Id = "AI812", Name = "資本適足率報表" },
        new() { Id = "AI813", Name = "槓桿比率表" },
        new() { Id = "AI814", Name = "流動性覆蓋比率表" },
        new() { Id = "AI823", Name = "淨穩定資金比率表" },
        new() { Id = "AI863", Name = "資產品質分析表" }
    };
}

/// <summary>
/// 版本資訊
/// </summary>
public class VersionInfo
{
    public string Version { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
}

/// <summary>
/// 代理程式資訊
/// </summary>
public class AgentInfo
{
    public string Version { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// 公告
/// </summary>
public class News
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public List<TagId>? TagIds { get; set; }
    public List<Attachment>? Attachments { get; set; }
}

public class TagId
{
    public string ReportId { get; set; } = string.Empty;
}

public class Attachment
{
    public string Url { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Size { get; set; }
}

/// <summary>
/// 公告查詢結果
/// </summary>
public class NewsPayload
{
    public int TotalPages { get; set; }
    public int Number { get; set; }
    public int Size { get; set; }
    public List<News> Content { get; set; } = new();
}

/// <summary>
/// 報表申報結果
/// </summary>
public class ReportDeclarationResult
{
    public int No { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string PeriodType { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string Month { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusType { get; set; } = string.Empty;
    public string DueTime { get; set; } = string.Empty;
    public string QueryTime { get; set; } = string.Empty;
    public string? DeclarationTime { get; set; }
    public string? RequestId { get; set; }
    public string? TransactionId { get; set; }
    public List<string>? Errors { get; set; }
}

/// <summary>
/// 報表歷程
/// </summary>
public class ReportHistory : ReportDeclarationResult
{
}

/// <summary>
/// 報表查詢結果
/// </summary>
public class ReportsPayload
{
    public List<ReportDeclarationResult> Reports { get; set; } = new();
}

/// <summary>
/// 報表歷程查詢結果
/// </summary>
public class ReportHistoriesPayload
{
    public List<ReportHistory> Reports { get; set; } = new();
}

public class ArchivedReportHistoryRecord
{
    public string BankCode { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string Year { get; set; } = string.Empty;
    public string? Type { get; set; }
    public ReportHistory Report { get; set; } = new();
    public DateTime ArchivedAtUtc { get; set; }
}

public class ArchivedReportHistoriesPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ArchivedReportHistoryRecord> Reports { get; set; } = new();
}

/// <summary>
/// AD 網域登入成功回傳
/// </summary>
public class AdLoginPayload
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public class AdminUser
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class AdminRole
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class AdminUsersPayload
{
    public List<AdminUser> Users { get; set; } = new();
}

public class AdminRolesPayload
{
    public List<AdminRole> Roles { get; set; } = new();
}

