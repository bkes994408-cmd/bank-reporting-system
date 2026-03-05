using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BankReporting.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet("roles")]
    public IActionResult GetRoles()
    {
        return Ok(new ApiResponse<AdminRolesPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = new AdminRolesPayload { Roles = _adminService.GetRoles().ToList() }
        });
    }

    [HttpGet("users")]
    public IActionResult GetUsers()
    {
        return Ok(new ApiResponse<AdminUsersPayload>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = new AdminUsersPayload { Users = _adminService.GetUsers().ToList() }
        });
    }

    [HttpPost("users")]
    public IActionResult CreateUser([FromBody] AdminCreateUserRequest request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { code = "ADMIN_INVALID_INPUT", msg = "Username 不可為空" });
        }

        var roles = request.Roles?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).ToList() ?? new List<string>();
        var result = _adminService.CreateUser(username, request.DisplayName ?? string.Empty, roles);

        return result.Code switch
        {
            "0000" => Ok(result),
            "ADMIN_USER_EXISTS" => Conflict(result),
            _ => BadRequest(result)
        };
    }

    [HttpPut("users/{username}/roles")]
    public IActionResult UpdateUserRoles(string username, [FromBody] AdminUpdateUserRolesRequest request)
    {
        var normalized = username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest(new { code = "ADMIN_INVALID_INPUT", msg = "Username 不可為空" });
        }

        var roles = request.Roles?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).ToList() ?? new List<string>();
        var result = _adminService.UpdateUserRoles(normalized, roles);

        return result.Code switch
        {
            "0000" => Ok(result),
            "ADMIN_USER_NOT_FOUND" => NotFound(result),
            _ => BadRequest(result)
        };
    }
}
