using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IAgentService
{
    Task<ApiResponse<object>> ParseExcelAsync(string reportId, IFormFile file);
    Task<ApiResponse<object>> ParseExcelWithContactAsync(ExcelWithContactRequest request, IFormFile file);
    Task<ApiResponse<object>> DeclareAsync(DeclareRequest request);
    Task<ApiResponse<ReportDeclarationResult>> GetDeclareResultAsync(DeclareResultRequest request);
    Task<ApiResponse<ReportsPayload>> GetMonthlyReportsAsync(MonthlyReportsRequest request);
    Task<ApiResponse<ReportHistoriesPayload>> GetReportHistoriesAsync(ReportHistoriesRequest request);
    Task<ApiResponse<object>> ImportKeysAsync(ImportKeysRequest request);
    Task<ApiResponse<object>> UpdateTokenAsync(UpdateTokenRequest request);
    Task<ApiResponse<VersionInfo>> CheckVersionAsync();
    Task<ApiResponse<AgentInfo>> GetAgentInfoAsync();
    Task<ApiResponse<NewsPayload>> GetNewsAsync(NewsRequest request);
    Task<ApiResponse<object>> ValidateKeysAsync();
    Task<byte[]> DownloadAttachmentAsync(AttachmentDownloadRequest request);
}

public class AgentService : IAgentService
{
    private const string InternalErrorCode = "5000";
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AgentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["AgentSettings:BaseUrl"] ?? "https://127.0.0.1:8005/APBSA";
    }

    public async Task<ApiResponse<object>> ParseExcelAsync(string reportId, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        AddStringContent(content, "reportId", reportId);

        using var fileStream = file.OpenReadStream();
        using var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "uploadFile", file.FileName);

        return await SendAsync<object>(() => _httpClient.PostAsync($"{_baseUrl}/agent-api/parsing/v1", content), "轉換失敗");
    }

    public async Task<ApiResponse<object>> ParseExcelWithContactAsync(ExcelWithContactRequest request, IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        AddStringContent(content, "bankCode", request.BankCode);
        AddStringContent(content, "bankName", request.BankName);
        AddStringContent(content, "reportYear", request.ReportYear);
        AddStringContent(content, "reportMonth", request.ReportMonth);
        AddStringContent(content, "contractorName", request.ContractorName);
        AddStringContent(content, "contractorTel", request.ContractorTel);
        AddStringContent(content, "contractorEmail", request.ContractorEmail);
        AddStringContent(content, "managerName", request.ManagerName);
        AddStringContent(content, "managerTel", request.ManagerTel);
        AddStringContent(content, "managerEmail", request.ManagerEmail);
        AddStringContent(content, "reportId", request.ReportId);

        using var fileStream = file.OpenReadStream();
        using var fileContent = new StreamContent(fileStream);
        content.Add(fileContent, "uploadFile", file.FileName);

        return await SendAsync<object>(() => _httpClient.PostAsync($"{_baseUrl}/agent-api/parsing/contact/v1", content), "轉換失敗");
    }

    public Task<ApiResponse<object>> DeclareAsync(DeclareRequest request)
        => SendAsync<object>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/declare/v1", request), "上傳失敗");

    public Task<ApiResponse<ReportDeclarationResult>> GetDeclareResultAsync(DeclareResultRequest request)
        => SendAsync<ReportDeclarationResult>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/declare/result/v1", request), "查詢失敗");

    public Task<ApiResponse<ReportsPayload>> GetMonthlyReportsAsync(MonthlyReportsRequest request)
        => SendAsync<ReportsPayload>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/reports/v1", request), "查詢失敗");

    public Task<ApiResponse<ReportHistoriesPayload>> GetReportHistoriesAsync(ReportHistoriesRequest request)
        => SendAsync<ReportHistoriesPayload>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/reports/histories/v1", request), "查詢失敗");

    public Task<ApiResponse<object>> ImportKeysAsync(ImportKeysRequest request)
        => SendAsync<object>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/keys/import", request), "匯入失敗");

    public Task<ApiResponse<object>> UpdateTokenAsync(UpdateTokenRequest request)
        => SendAsync<object>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/token/update", request), "更新失敗");

    public Task<ApiResponse<VersionInfo>> CheckVersionAsync()
        => SendAsync<VersionInfo>(() => _httpClient.GetAsync($"{_baseUrl}/agent-api/check-version"), "檢查失敗");

    public Task<ApiResponse<AgentInfo>> GetAgentInfoAsync()
        => SendAsync<AgentInfo>(() => _httpClient.GetAsync($"{_baseUrl}/agent-api/info/v1"), "查詢失敗");

    public Task<ApiResponse<NewsPayload>> GetNewsAsync(NewsRequest request)
        => SendAsync<NewsPayload>(() => _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/news/v1", request), "查詢失敗");

    public Task<ApiResponse<object>> ValidateKeysAsync()
        => SendAsync<object>(() => _httpClient.PostAsync($"{_baseUrl}/agent-api/keys/validate", null), "驗證失敗");

    public async Task<byte[]> DownloadAttachmentAsync(AttachmentDownloadRequest request)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/news/attachments/v1", request);
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private async Task<ApiResponse<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> sendRequest, string fallbackMessage)
    {
        try
        {
            using var response = await sendRequest();
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>();
            return result ?? BuildFailureResponse<T>(fallbackMessage);
        }
        catch (Exception ex)
        {
            return BuildFailureResponse<T>(ex.Message);
        }
    }

    private static ApiResponse<T> BuildFailureResponse<T>(string message)
        => new() { Code = InternalErrorCode, Msg = message };

    private static void AddStringContent(MultipartFormDataContent formData, string fieldName, string? value)
        => formData.Add(new StringContent(value ?? string.Empty), fieldName);
}
