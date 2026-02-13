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
/// 報表類型
/// </summary>
public static class ReportTypes
{
    public static readonly string[] All = new[]
    {
        "AI302", "AI330", "AI335", "AI341", "AI345", "AI346",
        "AI370", "AI372", "AI395", "AI397", "AI501", "AI505",
        "AI515", "AI520", "AI555", "AI560", "AI812", "AI813",
        "AI814", "AI823", "AI863"
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
