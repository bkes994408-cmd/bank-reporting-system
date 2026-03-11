using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IThirdPartyIntegrationService
{
    List<string> GetEnabledSystems();
    Task<ApiResponse<ThirdPartySyncResult>> SyncAsync(ThirdPartySyncRequest request);
    ThirdPartyDeadLetterPayload GetDeadLetters();
    Task<ApiResponse<ThirdPartySyncResult>> RetryDeadLetterAsync(string deadLetterId);
}

public class ThirdPartyIntegrationService : IThirdPartyIntegrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Dictionary<string, ThirdPartySystemConfig> _systems;
    private readonly ConcurrentDictionary<string, ThirdPartyDeadLetterRecord> _deadLetters = new(StringComparer.OrdinalIgnoreCase);

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

    public ThirdPartyDeadLetterPayload GetDeadLetters()
        => new()
        {
            Items = _deadLetters.Values
                .OrderByDescending(x => x.LastFailedAtUtc)
                .ToList()
        };

    public async Task<ApiResponse<ThirdPartySyncResult>> RetryDeadLetterAsync(string deadLetterId)
    {
        if (!_deadLetters.TryGetValue(deadLetterId, out var item))
        {
            return new ApiResponse<ThirdPartySyncResult>
            {
                Code = "4041",
                Msg = "找不到死信佇列紀錄"
            };
        }

        var result = await SyncPayloadAsync(item.Payload);
        if (result.Payload?.Success == true)
        {
            _deadLetters.TryRemove(deadLetterId, out _);
        }

        return result;
    }

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

        var syncPayload = BuildPayload(request, target.Name);
        return await SyncPayloadAsync(syncPayload);
    }

    private ThirdPartySyncPayload BuildPayload(ThirdPartySyncRequest request, string systemName)
        => new()
        {
            SystemName = systemName,
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

    private async Task<ApiResponse<ThirdPartySyncResult>> SyncPayloadAsync(ThirdPartySyncPayload syncPayload)
    {
        if (!_systems.TryGetValue(syncPayload.SystemName.Trim(), out var target) || !target.Enabled)
        {
            return new ApiResponse<ThirdPartySyncResult>
            {
                Code = "4040",
                Msg = "找不到可用的第三方系統設定"
            };
        }

        var maxAttempts = Math.Max(3, target.MaxRetries + 1);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(0, target.RetryDelayMilliseconds));

        string? lastErrorMessage = null;
        int lastStatusCode = 0;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await SendToTargetAsync(target, syncPayload);
                lastStatusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    return new ApiResponse<ThirdPartySyncResult>
                    {
                        Code = "0000",
                        Msg = "同步成功",
                        Payload = new ThirdPartySyncResult
                        {
                            SystemName = target.Name,
                            Success = true,
                            StatusCode = (int)response.StatusCode,
                            Message = "ok",
                            AttemptCount = attempt
                        }
                    };
                }

                lastErrorMessage = await response.Content.ReadAsStringAsync();
                var retryable = IsRetryableStatus(response.StatusCode, target);
                if (attempt < maxAttempts && retryable)
                {
                    await Task.Delay(retryDelay);
                    continue;
                }

                break;
            }
            catch (Exception ex)
            {
                lastErrorMessage = ex.Message;
                if (attempt < maxAttempts)
                {
                    await Task.Delay(retryDelay);
                    continue;
                }
            }
        }

        var deadLetter = await MoveToDeadLetterAsync(target, syncPayload, maxAttempts, lastStatusCode, lastErrorMessage ?? "unknown error");
        return new ApiResponse<ThirdPartySyncResult>
        {
            Code = "5020",
            Msg = "第三方系統同步失敗，已進入死信佇列",
            Payload = new ThirdPartySyncResult
            {
                SystemName = target.Name,
                Success = false,
                StatusCode = lastStatusCode,
                Message = lastErrorMessage ?? "unknown error",
                AttemptCount = maxAttempts,
                DeadLetterId = deadLetter.Id
            }
        };
    }

    private async Task<HttpResponseMessage> SendToTargetAsync(ThirdPartySystemConfig target, ThirdPartySyncPayload payload)
    {
        var client = _httpClientFactory.CreateClient();

        if (!string.IsNullOrWhiteSpace(target.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", target.ApiKey);
        }

        var baseUri = new Uri(target.BaseUrl.TrimEnd('/') + "/");
        var path = target.SyncPath.TrimStart('/');
        var endpoint = new Uri(baseUri, path);
        return await client.PostAsJsonAsync(endpoint, payload);
    }

    private static bool IsRetryableStatus(HttpStatusCode statusCode, ThirdPartySystemConfig target)
    {
        var status = (int)statusCode;
        var retryable = target.RetryableStatusCodes;
        if (retryable == null || retryable.Count == 0)
        {
            retryable = new List<int> { 408, 429, 500, 502, 503, 504 };
        }

        if (status >= 500)
        {
            return true;
        }

        return retryable.Contains(status);
    }

    private async Task<ThirdPartyDeadLetterRecord> MoveToDeadLetterAsync(
        ThirdPartySystemConfig target,
        ThirdPartySyncPayload payload,
        int attemptCount,
        int statusCode,
        string errorMessage)
    {
        var record = new ThirdPartyDeadLetterRecord
        {
            Payload = payload,
            ErrorCode = "5020",
            ErrorMessage = errorMessage,
            LastStatusCode = statusCode,
            AttemptCount = attemptCount,
            FirstFailedAtUtc = DateTimeOffset.UtcNow,
            LastFailedAtUtc = DateTimeOffset.UtcNow
        };

        if (!string.IsNullOrWhiteSpace(target.CompensationPath))
        {
            var compensateResult = await TryCompensateAsync(target, payload, errorMessage);
            record.CompensationExecuted = compensateResult.success;
            record.CompensationResult = compensateResult.message;
        }

        _deadLetters[record.Id] = record;
        return record;
    }

    private async Task<(bool success, string message)> TryCompensateAsync(ThirdPartySystemConfig target, ThirdPartySyncPayload payload, string reason)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            if (!string.IsNullOrWhiteSpace(target.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", target.ApiKey);
            }

            var baseUri = new Uri(target.BaseUrl.TrimEnd('/') + "/");
            var endpoint = new Uri(baseUri, target.CompensationPath!.TrimStart('/'));
            var compensationPayload = new
            {
                payload.SystemName,
                payload.EventType,
                payload.RequestId,
                payload.TransactionId,
                reason,
                triggeredAtUtc = DateTimeOffset.UtcNow
            };

            var response = await client.PostAsJsonAsync(endpoint, compensationPayload);
            if (response.IsSuccessStatusCode)
            {
                return (true, "補償成功");
            }

            return (false, $"補償失敗: {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, $"補償例外: {ex.Message}");
        }
    }
}
