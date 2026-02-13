using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.DTOs;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController : ControllerBase
{
    private readonly IAgentService _agentService;

    public NewsController(IAgentService agentService)
    {
        _agentService = agentService;
    }

    /// <summary>
    /// 查詢公告
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GetNews([FromBody] NewsRequest request)
    {
        var result = await _agentService.GetNewsAsync(request);
        return Ok(result);
    }

    /// <summary>
    /// 下載公告附件
    /// </summary>
    [HttpPost("attachments")]
    public async Task<IActionResult> DownloadAttachment([FromBody] AttachmentDownloadRequest request)
    {
        var fileBytes = await _agentService.DownloadAttachmentAsync(request);
        
        if (fileBytes.Length == 0)
        {
            return NotFound(new { code = "0001", msg = "檔案不存在" });
        }

        var contentType = request.Type switch
        {
            "PDF" => "application/pdf",
            "IMG" => "image/png",
            "WORD" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "EXCEL" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };

        return File(fileBytes, contentType, request.Name);
    }
}
