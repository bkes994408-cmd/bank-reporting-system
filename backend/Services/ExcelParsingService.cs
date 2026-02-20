using System.IO.Compression;
using System.Xml.Linq;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IExcelParsingService
{
    Task<ApiResponse<ExcelParsingPayload>> ParseAsync(string reportId, IFormFile? file);
}

public class ExcelParsingService : IExcelParsingService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx"
    };

    public async Task<ApiResponse<ExcelParsingPayload>> ParseAsync(string reportId, IFormFile? file)
    {
        if (file == null)
        {
            return new ApiResponse<ExcelParsingPayload>
            {
                Code = ParsingErrorCodes.MissingFile,
                Msg = "請上傳 Excel 檔案"
            };
        }

        var ext = Path.GetExtension(file.FileName ?? string.Empty);
        if (!AllowedExtensions.Contains(ext))
        {
            return new ApiResponse<ExcelParsingPayload>
            {
                Code = ParsingErrorCodes.InvalidFileType,
                Msg = "僅支援 .xlsx 檔案"
            };
        }

        if (file.Length <= 0)
        {
            return new ApiResponse<ExcelParsingPayload>
            {
                Code = ParsingErrorCodes.EmptyFile,
                Msg = "檔案為空"
            };
        }

        try
        {
            using var mem = new MemoryStream();
            await file.CopyToAsync(mem);
            mem.Position = 0;

            using var archive = new ZipArchive(mem, ZipArchiveMode.Read, leaveOpen: false);

            var workbookEntry = archive.GetEntry("xl/workbook.xml");
            var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
            if (workbookEntry == null || sheetEntry == null)
            {
                return new ApiResponse<ExcelParsingPayload>
                {
                    Code = ParsingErrorCodes.InvalidWorkbook,
                    Msg = "Excel 結構不正確，找不到必要工作簿內容"
                };
            }

            var sharedStrings = await LoadSharedStringsAsync(archive);
            var sheetName = await LoadFirstSheetNameAsync(workbookEntry) ?? "Sheet1";

            var payload = await ParseSheetAsync(sheetEntry, sharedStrings);
            if (payload.Headers.Count == 0)
            {
                return new ApiResponse<ExcelParsingPayload>
                {
                    Code = ParsingErrorCodes.InvalidWorksheet,
                    Msg = "工作表缺少標題列（第一列）"
                };
            }

            payload.ReportId = reportId;
            payload.SheetName = sheetName;

            return new ApiResponse<ExcelParsingPayload>
            {
                Code = "0000",
                Msg = "解析成功",
                Payload = payload
            };
        }
        catch (InvalidDataException)
        {
            return new ApiResponse<ExcelParsingPayload>
            {
                Code = ParsingErrorCodes.InvalidWorkbook,
                Msg = "檔案不是合法的 .xlsx 格式"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ExcelParsingPayload>
            {
                Code = ParsingErrorCodes.ParseFailed,
                Msg = $"解析失敗：{ex.Message}"
            };
        }
    }

    private static async Task<List<string>> LoadSharedStringsAsync(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry == null) return new List<string>();

        await using var stream = entry.Open();
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc.Descendants(ns + "si")
            .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value)))
            .ToList();
    }

    private static async Task<string?> LoadFirstSheetNameAsync(ZipArchiveEntry workbookEntry)
    {
        await using var stream = workbookEntry.Open();
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return doc.Descendants(ns + "sheet").FirstOrDefault()?.Attribute("name")?.Value;
    }

    private static async Task<ExcelParsingPayload> ParseSheetAsync(ZipArchiveEntry sheetEntry, List<string> sharedStrings)
    {
        await using var stream = sheetEntry.Open();
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, CancellationToken.None);

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = doc.Descendants(ns + "row").ToList();

        var payload = new ExcelParsingPayload();
        if (rows.Count == 0) return payload;

        var headerMap = ParseRow(rows[0], sharedStrings, ns);
        payload.Headers = headerMap.OrderBy(x => x.Key).Select(x => x.Value).ToList();

        foreach (var row in rows.Skip(1))
        {
            var rowCells = ParseRow(row, sharedStrings, ns);
            var record = new Dictionary<string, string>();

            for (var i = 0; i < payload.Headers.Count; i++)
            {
                var header = payload.Headers[i];
                record[header] = rowCells.TryGetValue(i, out var value) ? value : string.Empty;
            }

            payload.Rows.Add(record);
        }

        return payload;
    }

    private static Dictionary<int, string> ParseRow(XElement rowElement, List<string> sharedStrings, XNamespace ns)
    {
        var result = new Dictionary<int, string>();

        foreach (var cell in rowElement.Elements(ns + "c"))
        {
            var reference = cell.Attribute("r")?.Value;
            if (string.IsNullOrWhiteSpace(reference)) continue;

            var index = ColumnNameToIndex(new string(reference.TakeWhile(char.IsLetter).ToArray()));
            result[index] = ReadCellValue(cell, sharedStrings, ns);
        }

        return result;
    }

    private static string ReadCellValue(XElement cell, List<string> sharedStrings, XNamespace ns)
    {
        var type = cell.Attribute("t")?.Value;
        var value = cell.Element(ns + "v")?.Value ?? string.Empty;

        if (type == "s" && int.TryParse(value, out var idx) && idx >= 0 && idx < sharedStrings.Count)
        {
            return sharedStrings[idx];
        }

        if (type == "inlineStr")
        {
            return cell.Element(ns + "is")?.Element(ns + "t")?.Value ?? string.Empty;
        }

        return value;
    }

    private static int ColumnNameToIndex(string columnName)
    {
        var index = 0;
        foreach (var c in columnName)
        {
            index = index * 26 + (char.ToUpperInvariant(c) - 'A' + 1);
        }

        return index - 1;
    }
}
