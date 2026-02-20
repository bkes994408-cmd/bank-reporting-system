namespace BankReporting.Api.Models;

public static class ParsingErrorCodes
{
    public const string MissingFile = "PARSING_4001";
    public const string InvalidFileType = "PARSING_4002";
    public const string EmptyFile = "PARSING_4003";
    public const string InvalidWorkbook = "PARSING_4221";
    public const string InvalidWorksheet = "PARSING_4222";
    public const string ParseFailed = "PARSING_5000";
}

/// <summary>
/// Excel 解析結果
/// </summary>
public class ExcelParsingPayload
{
    /// <summary>
    /// 報表代碼（原樣回傳）
    /// </summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>
    /// 工作表名稱
    /// </summary>
    public string SheetName { get; set; } = string.Empty;

    /// <summary>
    /// 欄位名稱（第一列）
    /// </summary>
    public List<string> Headers { get; set; } = new();

    /// <summary>
    /// 資料列（由第二列起）
    /// </summary>
    public List<Dictionary<string, string>> Rows { get; set; } = new();

    public int RowCount => Rows.Count;
}
