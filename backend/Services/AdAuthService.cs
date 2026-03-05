using System.Security.Cryptography;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IAdAuthService
{
    Task<ApiResponse<AdLoginPayload>> LoginAsync(string username, string password);
}

public class AdAuthService : IAdAuthService
{
    private readonly IConfiguration _configuration;

    public AdAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<ApiResponse<AdLoginPayload>> LoginAsync(string username, string password)
    {
        var enabled = _configuration.GetValue<bool>("AdAuth:Enabled");
        if (!enabled)
        {
            return Task.FromResult(new ApiResponse<AdLoginPayload>
            {
                Code = "AUTH_DISABLED",
                Msg = "AD 登入功能未啟用"
            });
        }

        var normalizedUsername = (username ?? string.Empty).Trim();
        var normalizedPassword = password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedUsername) || string.IsNullOrWhiteSpace(normalizedPassword))
        {
            return Task.FromResult(new ApiResponse<AdLoginPayload>
            {
                Code = "AUTH_INVALID_INPUT",
                Msg = "帳號或密碼不可為空"
            });
        }

        var users = _configuration.GetSection("AdAuth:Users").Get<List<AdUserConfig>>() ?? new List<AdUserConfig>();
        var account = users.FirstOrDefault(u => string.Equals(u.Username, normalizedUsername, StringComparison.OrdinalIgnoreCase));

        if (account is null || !string.Equals(account.Password, normalizedPassword, StringComparison.Ordinal))
        {
            return Task.FromResult(new ApiResponse<AdLoginPayload>
            {
                Code = "AUTH_INVALID_CREDENTIALS",
                Msg = "網域帳號或密碼錯誤"
            });
        }

        var domain = _configuration["AdAuth:Domain"] ?? "LOCAL";
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        return Task.FromResult(new ApiResponse<AdLoginPayload>
        {
            Code = "0000",
            Msg = "登入成功",
            Payload = new AdLoginPayload
            {
                Username = account.Username,
                DisplayName = string.IsNullOrWhiteSpace(account.DisplayName) ? account.Username : account.DisplayName,
                Domain = domain,
                Roles = account.Roles ?? new List<string>(),
                AccessToken = token,
                ExpiresAtUtc = DateTime.UtcNow.AddHours(8)
            }
        });
    }

    private class AdUserConfig
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public List<string>? Roles { get; set; }
    }
}
