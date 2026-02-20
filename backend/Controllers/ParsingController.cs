using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/parsing")]
public class ParsingController : ControllerBase
{
    private readonly IAgentService _agentService;
    private readonly IExcelParsingService _excelParsingService;

    public ParsingController(IAgentService agentService, IExcelParsingService excelParsingService)
    {
        _agentService = agentService;
        _excelParsingService = excelParsingService;
    }

    /// <summary>
    /// Excel 轉 JSON（MVP）
    /// </summary>
    /// <remarks>
    /// Input 契約（multipart/form-data）：
    /// - reportId: string（可選，會原樣回傳）
    /// - uploadFile: .xlsx 檔案（必填）
    ///
    /// Excel 結構契約：
    /// - 解析第一個工作表（sheet1.xml）
    /// - 第一列視為欄位名稱（Headers）
    /// - 第二列起為資料列（Rows）
    /// </remarks>
    [HttpPost("excel")]
    [ProducesResponseType(typeof(ApiResponse<ExcelParsingPayload>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ParseExcel([FromForm] ExcelParsingRequest request)
    {
        var result = await _excelParsingService.ParseAsync(request.ReportId, request.UploadFile);

        return result.Code switch
        {
            "0000" => Ok(result),
            ParsingErrorCodes.MissingFile or ParsingErrorCodes.EmptyFile => BadRequest(new ApiResponse<object>
            {
                Code = result.Code,
                Msg = result.Msg
            }),
            ParsingErrorCodes.InvalidFileType => StatusCode(StatusCodes.Status415UnsupportedMediaType, new ApiResponse<object>
            {
                Code = result.Code,
                Msg = result.Msg
            }),
            ParsingErrorCodes.InvalidWorkbook or ParsingErrorCodes.InvalidWorksheet => UnprocessableEntity(new ApiResponse<object>
            {
                Code = result.Code,
                Msg = result.Msg
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError, new ApiResponse<object>
            {
                Code = result.Code,
                Msg = result.Msg
            })
        };
    }

    /// <summary>
    /// Excel + 聯絡人資訊轉JSON
    /// </summary>
    [HttpPost("excel-with-contact")]
    public async Task<IActionResult> ParseExcelWithContact([FromForm] ExcelWithContactRequest request)
    {
        if (request.UploadFile == null)
        {
            return BadRequest(new { code = "4000", msg = "請上傳Excel檔案" });
        }

        var result = await _agentService.ParseExcelWithContactAsync(request, request.UploadFile);
        return Ok(result);
    }
}
