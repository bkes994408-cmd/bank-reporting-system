using BankReporting.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.Services;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/crypto")]
public class CryptoController : ControllerBase
{
    private readonly IJweEncryptionService _jweEncryptionService;

    public CryptoController(IJweEncryptionService jweEncryptionService)
    {
        _jweEncryptionService = jweEncryptionService;
    }

    /// <summary>
    /// 直接進行 JWE 加密（不依賴代理程式）
    /// </summary>
    [HttpPost("jwe/encrypt")]
    public IActionResult EncryptJwe([FromBody] JweEncryptRequest request)
    {
        if (request.Payload is null)
        {
            return BadRequest(new { code = "4000", msg = "payload 為必填" });
        }

        if (string.IsNullOrWhiteSpace(request.PublicKeyPem))
        {
            return BadRequest(new { code = "4000", msg = "publicKeyPem 為必填" });
        }

        try
        {
            var compactJwe = _jweEncryptionService.EncryptToCompactJwe(request.Payload, request.PublicKeyPem.Trim(), request.KeyId);

            return Ok(new
            {
                code = "0000",
                msg = "JWE 加密成功",
                payload = new
                {
                    jwePayload = compactJwe,
                    alg = "RSA-OAEP-256",
                    enc = "A256GCM"
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = "4000", msg = $"JWE 加密失敗: {ex.Message}" });
        }
    }
}
