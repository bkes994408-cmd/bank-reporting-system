using System.Net.Http.Headers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IThirdPartyIntegrationService
{
    List<string> GetEnabledSystems();
    Task<ApiResponse<ThirdPartySyncResult>> SyncAsync(ThirdPartySyncRequest request);
}

public class ThirdPartyIntegrationService : IThirdPartyIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, ThirdPartySystemConfig> _systems;

    public ThirdPartyIntegrationService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _systems = configuration.GetSection("ThirdPartyIntegrations:Systems")
            .Get<List<ThirdPartySystemConfig>>()?
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .ToDictionary(x => x.Name.Trim(), x => x, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ThirdPartySystemConfig>(StringComparer.OrdinalIgnoreCase);
    }

    public List<string> GetEnabledSystems()
        => _systems.Values.Where(x => x.Enabled).Select(x => x.Name).OrderBy(x => x).ToList();

    public async Task<ApiResponse<ThirdPartySyncResult>> SyncAsync(ThirdPartySyncRequest request)
    {
        if (!_systems.TryGetValue(request.SystemName.Trim(), out var target) || !target.Enabled)
        {
            return new ApiResponse<ThirdPartySyncResult>
            {
                Code = "4040",
                Msg = "找不到可用的第三方系統設定"
            };
        }

        var syncPayload = new ThirdPartySyncPayload
        {
            SystemName = target.Name,
            EventType = request.EventType,
            BankCode = request.BankCode,
            ReportId = request.ReportId,
            Period = request.Period,
            Status = request.Status,
            RequestId = request.RequestId,
            TransactionId = request.TransactionId,
            Data = request.Data,
            SyncedAtUtc = DateTimeOffset.UtcNow
        };

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Max(1, target.TimeoutSeconds));

            if (!string.IsNullOrWhiteSpace(target.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", target.ApiKey);
            }

            var baseUri = new Uri(target.BaseUrl.TrimEnd('/') + "/");
            var path = target.SyncPath.TrimStart('/');
            var endpoint = new Uri(baseUri, path);
            var response = await client.PostAsJsonAsync(endpoint, syncPayload);

            return new ApiResponse<ThirdPartySyncResult>
            {
                Code = response.IsSuccessStatusCode ? "0000" : "5020",
                Msg = response.IsSuccessStatusCode ? "同步成功" : "第三方系統回應失敗",
                Payload = new ThirdPartySyncResult
                {
                    SystemName = target.Name,
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Message = response.IsSuccessStatusCode ? "ok" : await response.Content.ReadAsStringAsync()
                }
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ThirdPartySyncResult>
            {
                Code = "5000",
                Msg = "第三方系統同步失敗",
                Payload = new ThirdPartySyncResult
                {
                    SystemName = target.Name,
                    Success = false,
                    StatusCode = 0,
                    Message = ex.Message
                }
            };
        }
    }
}
