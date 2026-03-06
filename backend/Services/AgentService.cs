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
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public AgentService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["AgentSettings:BaseUrl"] ?? "https://127.0.0.1:8005/APBSA";
    }

    public async Task<ApiResponse<object>> ParseExcelAsync(string reportId, IFormFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(reportId), "reportId");
            
            using var fileStream = file.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            content.Add(fileContent, "uploadFile", file.FileName);

            var response = await _httpClient.PostAsync($"{_baseUrl}/agent-api/parsing/v1", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? new ApiResponse<object> { Code = "5000", Msg = "轉換失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<object>> ParseExcelWithContactAsync(ExcelWithContactRequest request, IFormFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(request.BankCode), "bankCode");
            content.Add(new StringContent(request.BankName), "bankName");
            content.Add(new StringContent(request.ReportYear), "reportYear");
            content.Add(new StringContent(request.ReportMonth), "reportMonth");
            content.Add(new StringContent(request.ContractorName), "contractorName");
            content.Add(new StringContent(request.ContractorTel), "contractorTel");
            content.Add(new StringContent(request.ContractorEmail), "contractorEmail");
            content.Add(new StringContent(request.ManagerName), "managerName");
            content.Add(new StringContent(request.ManagerTel), "managerTel");
            content.Add(new StringContent(request.ManagerEmail), "managerEmail");
            content.Add(new StringContent(request.ReportId), "reportId");
            
            using var fileStream = file.OpenReadStream();
            var fileContent = new StreamContent(fileStream);
            content.Add(fileContent, "uploadFile", file.FileName);

            var response = await _httpClient.PostAsync($"{_baseUrl}/agent-api/parsing/contact/v1", content);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? new ApiResponse<object> { Code = "5000", Msg = "轉換失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<object>> DeclareAsync(DeclareRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/declare/v1", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? new ApiResponse<object> { Code = "5000", Msg = "上傳失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<ReportDeclarationResult>> GetDeclareResultAsync(DeclareResultRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/declare/result/v1", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReportDeclarationResult>>();
            return result ?? new ApiResponse<ReportDeclarationResult> { Code = "5000", Msg = "查詢失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportDeclarationResult> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<ReportsPayload>> GetMonthlyReportsAsync(MonthlyReportsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/reports/v1", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReportsPayload>>();
            return result ?? new ApiResponse<ReportsPayload> { Code = "5000", Msg = "查詢失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportsPayload> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<ReportHistoriesPayload>> GetReportHistoriesAsync(ReportHistoriesRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/reports/histories/v1", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ReportHistoriesPayload>>();
            return result ?? new ApiResponse<ReportHistoriesPayload> { Code = "5000", Msg = "查詢失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<ReportHistoriesPayload> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<object>> ImportKeysAsync(ImportKeysRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/keys/import", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? new ApiResponse<object> { Code = "5000", Msg = "匯入失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<object>> UpdateTokenAsync(UpdateTokenRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/token/update", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? new ApiResponse<object> { Code = "5000", Msg = "更新失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<VersionInfo>> CheckVersionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/agent-api/check-version");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<VersionInfo>>();
            return result ?? new ApiResponse<VersionInfo> { Code = "5000", Msg = "檢查失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<VersionInfo> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<AgentInfo>> GetAgentInfoAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/agent-api/info/v1");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AgentInfo>>();
            return result ?? new ApiResponse<AgentInfo> { Code = "5000", Msg = "查詢失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<AgentInfo> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<NewsPayload>> GetNewsAsync(NewsRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/news/v1", request);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<NewsPayload>>();
            return result ?? new ApiResponse<NewsPayload> { Code = "5000", Msg = "查詢失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<NewsPayload> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<ApiResponse<object>> ValidateKeysAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync($"{_baseUrl}/agent-api/keys/validate", null);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
            return result ?? new ApiResponse<object> { Code = "5000", Msg = "驗證失敗" };
        }
        catch (Exception ex)
        {
            return new ApiResponse<object> { Code = "5000", Msg = ex.Message };
        }
    }

    public async Task<byte[]> DownloadAttachmentAsync(AttachmentDownloadRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/agent-api/news/attachments/v1", request);
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }
}
