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
        var mockExcelParsingService = new Mock<IExcelParsingService>();
        await using var app = new TestAppFactory(mockAgentService, mockExcelParsingService);
        using var client = app.CreateClient();

        var resp = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task HappyPath_Parse_Declare_QueryMonthlyReports_ReturnsOk()
    {
        // Arrange
        var mockAgentService = new Mock<IAgentService>(MockBehavior.Strict);
        var mockExcelParsingService = new Mock<IExcelParsingService>(MockBehavior.Strict);

        mockExcelParsingService
            .Setup(x => x.ParseAsync("AI330", It.IsAny<IFormFile>()))
            .ReturnsAsync(new ApiResponse<ExcelParsingPayload>
            {
                Code = "0000",
                Msg = "parse ok",
                Payload = new ExcelParsingPayload
                {
                    ReportId = "AI330",
                    Headers = ["c1"],
                    Rows =
                    [
                        new Dictionary<string, string>
                        {
                            ["c1"] = "v1"!
                        }
                    ]
                }
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
            .Setup(x => x.GetMonthlyReportsAsync(It.Is<MonthlyReportsRequest>(r =>
                r.BankCode == "0070000" && r.ApplyYear == "113" && r.ApplyMonth == "01")))
            .ReturnsAsync(new ApiResponse<ReportsPayload>
            {
                Code = "0000",
                Msg = "query ok",
                Payload = new ReportsPayload
                {
                    Reports =
                    [
                        new ReportDeclarationResult
                        {
                            BankCode = "0070000",
                            ReportId = "AI330",
                            Year = "113",
                            Month = "01",
                            Status = "SUCCESS",
                            StatusType = "SUCCESS"
                        }
                    ]
                }
            });

        await using var app = new TestAppFactory(mockAgentService, mockExcelParsingService);
        using var client = app.CreateClient();

        // 1) 解析流程: /api/parsing/excel
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

        // 2) 申報流程: /api/declare
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

        // 3) 查詢流程: /api/reports
        var queryReq = new MonthlyReportsRequest
        {
            BankCode = "0070000",
            ApplyYear = "113",
            ApplyMonth = "01"
        };

        var queryResp = await client.PostAsJsonAsync("/api/reports", queryReq);
        Assert.Equal(HttpStatusCode.OK, queryResp.StatusCode);

        var queryBody = await queryResp.Content.ReadFromJsonAsync<ApiResponse<ReportsPayload>>();
        Assert.NotNull(queryBody);
        Assert.Equal("0000", queryBody!.Code);
        Assert.NotNull(queryBody.Payload);
        Assert.Single(queryBody.Payload!.Reports);
        Assert.Equal("AI330", queryBody.Payload.Reports[0].ReportId);

        mockAgentService.VerifyAll();
        mockExcelParsingService.VerifyAll();
    }

    private sealed class TestAppFactory : WebApplicationFactory<Program>
    {
        private readonly Mock<IAgentService> _mockAgentService;
        private readonly Mock<IExcelParsingService> _mockExcelParsingService;

        public TestAppFactory(Mock<IAgentService> mockAgentService, Mock<IExcelParsingService> mockExcelParsingService)
        {
            _mockAgentService = mockAgentService;
            _mockExcelParsingService = mockExcelParsingService;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Test");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IAgentService>();
                services.RemoveAll<IExcelParsingService>();
                services.AddSingleton(_mockAgentService.Object);
                services.AddSingleton(_mockExcelParsingService.Object);
            });
        }
    }
}
