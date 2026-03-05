using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IAccountAdminService
{
    Task<ApiResponse<List<AccountSummary>>> GetAccountsAsync();
    Task<ApiResponse<AccountSummary>> UpdateRolesAsync(string username, List<string> roles);
}

public class AccountAdminService : IAccountAdminService
{
    private readonly Dictionary<string, AccountSummary> _accounts;

    public AccountAdminService(IConfiguration configuration)
    {
        var users = configuration.GetSection("AdAuth:Users").Get<List<AdUserConfig>>() ?? new List<AdUserConfig>();

        _accounts = users
            .Where(u => !string.IsNullOrWhiteSpace(u.Username))
            .Select(u => new AccountSummary
            {
                Username = u.Username.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(u.DisplayName) ? u.Username.Trim() : u.DisplayName.Trim(),
                Roles = NormalizeRoles(u.Roles)
            })
            .GroupBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
    }

    public Task<ApiResponse<List<AccountSummary>>> GetAccountsAsync()
    {
        var payload = _accounts.Values
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .Select(Clone)
            .ToList();

        return Task.FromResult(new ApiResponse<List<AccountSummary>>
        {
            Code = "0000",
            Msg = "查詢成功",
            Payload = payload
        });
    }

    public Task<ApiResponse<AccountSummary>> UpdateRolesAsync(string username, List<string> roles)
    {
        var normalizedUsername = (username ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return Task.FromResult(new ApiResponse<AccountSummary>
            {
                Code = "ACCOUNT_INVALID_INPUT",
                Msg = "帳號不可為空"
            });
        }

        if (!_accounts.TryGetValue(normalizedUsername, out var account))
        {
            return Task.FromResult(new ApiResponse<AccountSummary>
            {
                Code = "ACCOUNT_NOT_FOUND",
                Msg = "找不到該帳號"
            });
        }

        account.Roles = NormalizeRoles(roles);

        return Task.FromResult(new ApiResponse<AccountSummary>
        {
            Code = "0000",
            Msg = "更新成功",
            Payload = Clone(account)
        });
    }

    private static List<string> NormalizeRoles(List<string>? roles)
    {
        return (roles ?? new List<string>())
            .Select(r => r?.Trim() ?? string.Empty)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AccountSummary Clone(AccountSummary account)
    {
        return new AccountSummary
        {
            Username = account.Username,
            DisplayName = account.DisplayName,
            Roles = account.Roles.ToList()
        };
    }

    private class AdUserConfig
    {
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public List<string>? Roles { get; set; }
    }
}
