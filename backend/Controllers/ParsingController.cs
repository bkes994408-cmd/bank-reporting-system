using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/parsing")]
public class ParsingController : ControllerBase
{
    private readonly IAgentService _agentService;

    public ParsingController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// Excel轉JSON
    /// </summary>
    [HttpPost("excel")]
    public async Task<IActionResult> ParseExcel([FromForm] ExcelParsingRequest request)
    {
        if (request.UploadFile == null)
        {
            return BadRequest(new { code = "4000", msg = "請上傳Excel檔案" });
        }

        var result = await _agentService.ParseExcelAsync(request.ReportId, request.UploadFile);
        return Ok(result);
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
