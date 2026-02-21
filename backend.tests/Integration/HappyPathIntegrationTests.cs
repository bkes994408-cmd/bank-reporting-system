using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace BankReporting.Tests.Integration;

public class HappyPathIntegrationTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        var mockAgentService = new Mock<IAgentService>();
        await using var app = new TestAppFactory(mockAgentService);
        using var client = app.CreateClient();

        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task HappyPath_ParseExcel_Declare_DeclareResult_ReturnsOk()
    {
        // Arrange
        var mockAgentService = new Mock<IAgentService>(MockBehavior.Strict);

        mockAgentService
            .Setup(x => x.ParseExcelAsync("AI330", It.IsAny<IFormFile>()))
            .ReturnsAsync(new ApiResponse<object>
            {
                Code = "0000",
                Msg = "parse ok",
                Payload = new { reportId = "AI330", parsed = true }
            });

        mockAgentService
            .Setup(x => x.DeclareAsync(It.Is<DeclareRequest>(r => r.RequestId == "0070000-123" && r.ReportId == "AI330")))
            .ReturnsAsync(new ApiResponse<object>
            {
                Code = "0000",
                Msg = "declare ok",
                Payload = new { transactionId = "tx-001" }
            });

        mockAgentService
            .Setup(x => x.GetDeclareResultAsync(It.Is<DeclareResultRequest>(r => r.RequestId == "0070000-123")))
            .ReturnsAsync(new ApiResponse<ReportDeclarationResult>
            {
                Code = "0000",
                Msg = "result ok",
                Payload = new ReportDeclarationResult
                {
                    RequestId = "0070000-123",
                    TransactionId = "tx-001",
                    BankCode = "0070000",
                    ReportId = "AI330",
                    Year = "113",
                    Month = "01",
                    Status = "SUCCESS",
                    StatusType = "SUCCESS"
                }
            });

        await using var app = new TestAppFactory(mockAgentService);
        using var client = app.CreateClient();

        // 1) /api/parsing/excel
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("AI330"), "ReportId");

        var fakeExcel = new ByteArrayContent(new byte[] { 0x50, 0x4B, 0x03, 0x04 }); // minimal zip header
        fakeExcel.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(fakeExcel, "UploadFile", "test.xlsx");

        var parseResp = await client.PostAsync("/api/parsing/excel", form);
        Assert.Equal(HttpStatusCode.OK, parseResp.StatusCode);

        var parseBody = await parseResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(parseBody);
        Assert.Equal("0000", parseBody!.Code);

        // 2) /api/declare
        var declareReq = new DeclareRequest
        {
            RequestId = "0070000-123",
            BankCode = "0070000",
            BankName = "第一銀行",
            ReportYear = "113",
            ReportMonth = "01",
            ReportId = "AI330",
            ContractorName = "測試人員",
            ContractorTel = "02-12345678",
            ContractorEmail = "test@test.com",
            ManagerName = "測試主管",
            ManagerTel = "02-12345679",
            ManagerEmail = "manager@test.com",
            Report = new { any = "data" }
        };

        var declareResp = await client.PostAsJsonAsync("/api/declare", declareReq);
        Assert.Equal(HttpStatusCode.OK, declareResp.StatusCode);

        var declareBody = await declareResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(declareBody);
        Assert.Equal("0000", declareBody!.Code);

        // 3) /api/declare/result
        var resultReq = new DeclareResultRequest { RequestId = "0070000-123" };
        var resultResp = await client.PostAsJsonAsync("/api/declare/result", resultReq);
        Assert.Equal(HttpStatusCode.OK, resultResp.StatusCode);

        var resultBody = await resultResp.Content.ReadFromJsonAsync<ApiResponse<ReportDeclarationResult>>();
        Assert.NotNull(resultBody);
        Assert.Equal("0000", resultBody!.Code);
        Assert.NotNull(resultBody.Payload);
        Assert.Equal("SUCCESS", resultBody.Payload!.Status);

        mockAgentService.VerifyAll();
    }


    [Fact]
    public async Task KeysEndpoints_ImportAndValidate_ReturnsOk()
    {
        // Arrange
        var mockAgentService = new Mock<IAgentService>(MockBehavior.Strict);

        mockAgentService
            .Setup(x => x.ImportKeysAsync(It.Is<ImportKeysRequest>(r => r.KeyA == "keyA123" && r.KeyB == "keyB456")))
            .ReturnsAsync(new ApiResponse<object>
            {
                Code = "0000",
                Msg = "import ok"
            });

        mockAgentService
            .Setup(x => x.ValidateKeysAsync())
            .ReturnsAsync(new ApiResponse<object>
            {
                Code = "0000",
                Msg = "validate ok"
            });

        await using var app = new TestAppFactory(mockAgentService);
        using var client = app.CreateClient();

        // 1) /api/keys/import
        var importReq = new ImportKeysRequest { KeyA = "  keyA123 ", KeyB = " keyB456  " };
        var importResp = await client.PostAsJsonAsync("/api/keys/import", importReq);
        Assert.Equal(HttpStatusCode.OK, importResp.StatusCode);

        var importBody = await importResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(importBody);
        Assert.Equal("0000", importBody!.Code);

        // 2) /api/keys/validate
        var validateResp = await client.PostAsync("/api/keys/validate", null);
        Assert.Equal(HttpStatusCode.OK, validateResp.StatusCode);

        var validateBody = await validateResp.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(validateBody);
        Assert.Equal("0000", validateBody!.Code);

        mockAgentService.VerifyAll();
    }

    private sealed class TestAppFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IAgentService> _mockAgentService;

        public TestAppFactory(Mock<IAgentService> mockAgentService)
        {
            _mockAgentService = mockAgentService;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgentService>();
                services.AddSingleton(_mockAgentService.Object);
            });
        }
    }
}
