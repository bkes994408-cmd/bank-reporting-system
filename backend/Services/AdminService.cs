using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IAdminService
{
    IReadOnlyList<AdminRole> GetRoles();
    IReadOnlyList<AdminUser> GetUsers();
    ApiResponse<AdminUser> CreateUser(string username, string displayName, List<string> roles);
    ApiResponse<AdminUser> UpdateUserRoles(string username, List<string> roles);
}

public class AdminService : IAdminService
{
    private static readonly List<AdminRole> BuiltInRoles = new()
    {
        new AdminRole { Name = "admin", Description = "系統管理者" },
        new AdminRole { Name = "reporter", Description = "申報作業人員" },
        new AdminRole { Name = "viewer", Description = "唯讀檢視" }
    };

    private readonly List<AdminUser> _users = new()
    {
        new AdminUser { Username = "demo", DisplayName = "Demo User", Roles = new List<string> { "admin" } }
    };

    public IReadOnlyList<AdminRole> GetRoles() => BuiltInRoles;

    public IReadOnlyList<AdminUser> GetUsers() => _users;

    public ApiResponse<AdminUser> CreateUser(string username, string displayName, List<string> roles)
    {
        if (_users.Any(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
        {
            return new ApiResponse<AdminUser> { Code = "ADMIN_USER_EXISTS", Msg = "使用者已存在" };
        }

        if (roles.Any(r => !BuiltInRoles.Any(x => x.Name.Equals(r, StringComparison.OrdinalIgnoreCase))))
        {
            return new ApiResponse<AdminUser> { Code = "ADMIN_INVALID_ROLE", Msg = "包含不存在的角色" };
        }

        var user = new AdminUser
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName,
            Roles = roles.Select(r => r.Trim().ToLowerInvariant()).Distinct().ToList()
        };

        _users.Add(user);
        return new ApiResponse<AdminUser> { Code = "0000", Msg = "新增成功", Payload = user };
    }

    public ApiResponse<AdminUser> UpdateUserRoles(string username, List<string> roles)
    {
        var user = _users.FirstOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
        if (user is null)
        {
            return new ApiResponse<AdminUser> { Code = "ADMIN_USER_NOT_FOUND", Msg = "使用者不存在" };
        }

        if (roles.Any(r => !BuiltInRoles.Any(x => x.Name.Equals(r, StringComparison.OrdinalIgnoreCase))))
        {
            return new ApiResponse<AdminUser> { Code = "ADMIN_INVALID_ROLE", Msg = "包含不存在的角色" };
        }

        user.Roles = roles.Select(r => r.Trim().ToLowerInvariant()).Distinct().ToList();
        return new ApiResponse<AdminUser> { Code = "0000", Msg = "更新成功", Payload = user };
    }
}
