using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankReporting.Tests;

public class IntelligentReportAutomationServiceTests
{
    [Fact]
    public async Task AutoGenerateAndSubmitAsync_WithSupportedReport_SubmitsSuccessfully()
    {
        var service = new IntelligentReportAutomationService(new SuccessAgentService());

        var result = await service.AutoGenerateAndSubmitAsync(new IntelligentReportAutoSubmitRequest
        {
            BankCode = "0070000",
            BankName = "第一銀行",
            ReportYear = "114",
            ReportMonth = "03",
            ReportId = "AI302",
            ContractorName = "王小明",
            ContractorTel = "02-12345678",
            ContractorEmail = "ops@example.com",
            ManagerName = "陳主管",
            ManagerTel = "02-12345679",
            ManagerEmail = "manager@example.com",
            DryRun = false,
            SourceData = new Dictionary<string, object>
            {
                ["assets"] = 200000000m,
                ["liabilities"] = 150000000m,
                ["equity"] = 50000000m
            }
        }, CancellationToken.None);

        Assert.Equal("submitted", result.Status);
        Assert.Equal("0000", result.SubmissionCode);
        Assert.StartsWith("0070000-11403-AI302-", result.RequestId);
    }

    [Fact]
    public async Task AutoGenerateAndSubmitAsync_WithDryRun_DoesNotCallSubmit()
    {
        var service = new IntelligentReportAutomationService(new FailAgentService());

        var result = await service.AutoGenerateAndSubmitAsync(new IntelligentReportAutoSubmitRequest
        {
            BankCode = "0070000",
            BankName = "第一銀行",
            ReportYear = "114",
            ReportMonth = "03",
            ReportId = "AI501",
            ContractorName = "王小明",
            ContractorTel = "02-12345678",
            ContractorEmail = "ops@example.com",
            ManagerName = "陳主管",
            ManagerTel = "02-12345679",
            ManagerEmail = "manager@example.com",
            DryRun = true
        }, CancellationToken.None);

        Assert.Equal("dry-run", result.Status);
        Assert.Null(result.SubmissionCode);
        Assert.Contains(result.ValidationWarnings, x => x.Contains("sourceData"));
    }

    [Fact]
    public async Task AutoGenerateAndSubmitAsync_WithUnsupportedReport_Throws()
    {
        var service = new IntelligentReportAutomationService(new SuccessAgentService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AutoGenerateAndSubmitAsync(new IntelligentReportAutoSubmitRequest
        {
            BankCode = "0070000",
            BankName = "第一銀行",
            ReportYear = "114",
            ReportMonth = "03",
            ReportId = "AI999",
            ContractorName = "王小明",
            ContractorTel = "02-12345678",
            ContractorEmail = "ops@example.com",
            ManagerName = "陳主管",
            ManagerTel = "02-12345679",
            ManagerEmail = "manager@example.com"
        }, CancellationToken.None));
    }
}

public class IntelligentReportsControllerTests
{
    [Fact]
    public async Task AutoSubmit_ReturnsBadRequest_WhenRequiredFieldMissing()
    {
        var controller = new IntelligentReportsController(new IntelligentReportAutomationService(new SuccessAgentService()));

        var result = await controller.AutoSubmit(new IntelligentReportAutoSubmitRequest
        {
            BankCode = "0070000",
            BankName = "",
            ReportYear = "114",
            ReportMonth = "03",
            ReportId = "AI302"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task AutoSubmit_ReturnsOk_WhenSubmitted()
    {
        var controller = new IntelligentReportsController(new IntelligentReportAutomationService(new SuccessAgentService()));

        var result = await controller.AutoSubmit(new IntelligentReportAutoSubmitRequest
        {
            BankCode = "0070000",
            BankName = "第一銀行",
            ReportYear = "114",
            ReportMonth = "03",
            ReportId = "AI812",
            ContractorName = "王小明",
            ContractorTel = "02-12345678",
            ContractorEmail = "ops@example.com",
            ManagerName = "陳主管",
            ManagerTel = "02-12345679",
            ManagerEmail = "manager@example.com"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<IntelligentReportSubmissionRecord>>(ok.Value);
        Assert.Equal("0000", payload.Code);
        Assert.Equal("submitted", payload.Payload!.Status);
    }
}

file sealed class SuccessAgentService : IAgentService
{
    public Task<ApiResponse<object>> ParseExcelAsync(string reportId, IFormFile file) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<object>> ParseExcelWithContactAsync(ExcelWithContactRequest request, IFormFile file) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<object>> DeclareAsync(DeclareRequest request) => Task.FromResult(new ApiResponse<object> { Code = "0000", Msg = "ok" });
    public Task<ApiResponse<ReportDeclarationResult>> GetDeclareResultAsync(DeclareResultRequest request) => Task.FromResult(new ApiResponse<ReportDeclarationResult>());
    public Task<ApiResponse<ReportsPayload>> GetMonthlyReportsAsync(MonthlyReportsRequest request) => Task.FromResult(new ApiResponse<ReportsPayload>());
    public Task<ApiResponse<ReportHistoriesPayload>> GetReportHistoriesAsync(ReportHistoriesRequest request) => Task.FromResult(new ApiResponse<ReportHistoriesPayload>());
    public Task<ApiResponse<object>> ImportKeysAsync(ImportKeysRequest request) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<object>> UpdateTokenAsync(UpdateTokenRequest request) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<VersionInfo>> CheckVersionAsync() => Task.FromResult(new ApiResponse<VersionInfo>());
    public Task<ApiResponse<AgentInfo>> GetAgentInfoAsync() => Task.FromResult(new ApiResponse<AgentInfo>());
    public Task<ApiResponse<NewsPayload>> GetNewsAsync(NewsRequest request) => Task.FromResult(new ApiResponse<NewsPayload>());
    public Task<ApiResponse<object>> ValidateKeysAsync() => Task.FromResult(new ApiResponse<object>());
    public Task<byte[]> DownloadAttachmentAsync(AttachmentDownloadRequest request) => Task.FromResult(Array.Empty<byte>());
}

file sealed class FailAgentService : IAgentService
{
    public Task<ApiResponse<object>> ParseExcelAsync(string reportId, IFormFile file) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<object>> ParseExcelWithContactAsync(ExcelWithContactRequest request, IFormFile file) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<object>> DeclareAsync(DeclareRequest request) => Task.FromResult(new ApiResponse<object> { Code = "5000", Msg = "should not call" });
    public Task<ApiResponse<ReportDeclarationResult>> GetDeclareResultAsync(DeclareResultRequest request) => Task.FromResult(new ApiResponse<ReportDeclarationResult>());
    public Task<ApiResponse<ReportsPayload>> GetMonthlyReportsAsync(MonthlyReportsRequest request) => Task.FromResult(new ApiResponse<ReportsPayload>());
    public Task<ApiResponse<ReportHistoriesPayload>> GetReportHistoriesAsync(ReportHistoriesRequest request) => Task.FromResult(new ApiResponse<ReportHistoriesPayload>());
    public Task<ApiResponse<object>> ImportKeysAsync(ImportKeysRequest request) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<object>> UpdateTokenAsync(UpdateTokenRequest request) => Task.FromResult(new ApiResponse<object>());
    public Task<ApiResponse<VersionInfo>> CheckVersionAsync() => Task.FromResult(new ApiResponse<VersionInfo>());
    public Task<ApiResponse<AgentInfo>> GetAgentInfoAsync() => Task.FromResult(new ApiResponse<AgentInfo>());
    public Task<ApiResponse<NewsPayload>> GetNewsAsync(NewsRequest request) => Task.FromResult(new ApiResponse<NewsPayload>());
    public Task<ApiResponse<object>> ValidateKeysAsync() => Task.FromResult(new ApiResponse<object>());
    public Task<byte[]> DownloadAttachmentAsync(AttachmentDownloadRequest request) => Task.FromResult(Array.Empty<byte>());
}
