using Xunit;
using Moq;
using Microsoft.AspNetCore.Mvc;
using BankReporting.Api.Controllers;
using BankReporting.Api.Services;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using Microsoft.Extensions.Configuration;

namespace BankReporting.Tests;

public class ParsingControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly ParsingController _controller;

    public ParsingControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _controller = new ParsingController(_mockAgentService.Object);
    }

    [Fact]
    public async Task ParseExcel_WithoutFile_ReturnsBadRequest()
    {
        // Arrange
        var request = new ExcelParsingRequest { ReportId = "AI330", UploadFile = null };

        // Act
        var result = await _controller.ParseExcel(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }
}

public class DeclareControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly DeclareController _controller;

    public DeclareControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _controller = new DeclareController(_mockAgentService.Object);
    }

    [Fact]
    public async Task Declare_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new DeclareRequest
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
            ManagerEmail = "manager@test.com"
        };

        _mockAgentService
            .Setup(x => x.DeclareAsync(It.IsAny<DeclareRequest>()))
            .ReturnsAsync(new ApiResponse<object> { Code = "0000", Msg = "上傳成功" });

        // Act
        var result = await _controller.Declare(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetDeclareResult_WithoutIds_ReturnsBadRequest()
    {
        // Arrange
        var request = new DeclareResultRequest { RequestId = null, TransactionId = null };

        // Act
        var result = await _controller.GetDeclareResult(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetDeclareResult_WithRequestId_ReturnsOk()
    {
        // Arrange
        var request = new DeclareResultRequest { RequestId = "0070000-123" };

        _mockAgentService
            .Setup(x => x.GetDeclareResultAsync(It.IsAny<DeclareResultRequest>()))
            .ReturnsAsync(new ApiResponse<ReportDeclarationResult> { Code = "0000", Msg = "查詢成功" });

        // Act
        var result = await _controller.GetDeclareResult(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}

public class ReportsControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly ReportsController _controller;

    public ReportsControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _controller = new ReportsController(_mockAgentService.Object);
    }

    [Fact]
    public async Task GetMonthlyReports_ReturnsOk()
    {
        // Arrange
        var request = new MonthlyReportsRequest
        {
            BankCode = "0070000",
            ApplyYear = "113",
            ApplyMonth = "01"
        };

        _mockAgentService
            .Setup(x => x.GetMonthlyReportsAsync(It.IsAny<MonthlyReportsRequest>()))
            .ReturnsAsync(new ApiResponse<ReportsPayload> { Code = "0000", Msg = "查詢成功" });

        // Act
        var result = await _controller.GetMonthlyReports(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetReportHistories_ReturnsOk()
    {
        // Arrange
        var request = new ReportHistoriesRequest
        {
            BankCode = "0070000",
            ReportId = "AI330",
            Year = "113"
        };

        _mockAgentService
            .Setup(x => x.GetReportHistoriesAsync(It.IsAny<ReportHistoriesRequest>()))
            .ReturnsAsync(new ApiResponse<ReportHistoriesPayload> { Code = "0000", Msg = "查詢成功" });

        // Act
        var result = await _controller.GetReportHistories(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}

public class KeysControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly KeysController _controller;

    public KeysControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _controller = new KeysController(_mockAgentService.Object);
    }

    [Fact]
    public async Task ImportKeys_WithEmptyKeys_ReturnsBadRequest()
    {
        // Arrange
        var request = new ImportKeysRequest { KeyA = "", KeyB = "" };

        // Act
        var result = await _controller.ImportKeys(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ImportKeys_WithValidKeys_ReturnsOk()
    {
        // Arrange
        var request = new ImportKeysRequest { KeyA = "keyA123", KeyB = "keyB456" };

        _mockAgentService
            .Setup(x => x.ImportKeysAsync(It.IsAny<ImportKeysRequest>()))
            .ReturnsAsync(new ApiResponse<object> { Code = "0000", Msg = "匯入成功" });

        // Act
        var result = await _controller.ImportKeys(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ValidateKeys_ReturnsOk()
    {
        // Arrange
        _mockAgentService
            .Setup(x => x.ValidateKeysAsync())
            .ReturnsAsync(new ApiResponse<object> { Code = "0000", Msg = "驗證成功" });

        // Act
        var result = await _controller.ValidateKeys();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}

public class TokenControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly TokenController _controller;

    public TokenControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _controller = new TokenController(_mockAgentService.Object);
    }

    [Fact]
    public async Task UpdateToken_WithEmptyToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new UpdateTokenRequest { Token = "" };

        // Act
        var result = await _controller.UpdateToken(request);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task UpdateToken_WithValidToken_ReturnsOk()
    {
        // Arrange
        var request = new UpdateTokenRequest { Token = "valid-token-123" };

        _mockAgentService
            .Setup(x => x.UpdateTokenAsync(It.IsAny<UpdateTokenRequest>()))
            .ReturnsAsync(new ApiResponse<object> { Code = "0000", Msg = "更新成功" });

        // Act
        var result = await _controller.UpdateToken(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}

public class NewsControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly NewsController _controller;

    public NewsControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _controller = new NewsController(_mockAgentService.Object);
    }

    [Fact]
    public async Task GetNews_ReturnsOk()
    {
        // Arrange
        var request = new NewsRequest { PageNumber = 0, PageSize = 10 };

        _mockAgentService
            .Setup(x => x.GetNewsAsync(It.IsAny<NewsRequest>()))
            .ReturnsAsync(new ApiResponse<NewsPayload>
            {
                Code = "0000",
                Msg = "查詢成功",
                Payload = new NewsPayload
                {
                    TotalPages = 1,
                    Number = 0,
                    Size = 10,
                    Content = new List<News>()
                }
            });

        // Act
        var result = await _controller.GetNews(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task DownloadAttachment_WithInvalidUrl_ReturnsNotFound()
    {
        // Arrange
        var request = new AttachmentDownloadRequest
        {
            Url = "/invalid/url",
            Name = "test.pdf",
            Type = "PDF"
        };

        _mockAgentService
            .Setup(x => x.DownloadAttachmentAsync(It.IsAny<AttachmentDownloadRequest>()))
            .ReturnsAsync(Array.Empty<byte>());

        // Act
        var result = await _controller.DownloadAttachment(request);

        // Assert
        Assert.IsType<NotFoundObjectResult>(result);
    }
}

public class MonitoringControllerTests
{
    [Fact]
    public void Metrics_ReturnsPrometheusPlainText()
    {
        // Arrange
        var monitoringService = new MonitoringService();
        monitoringService.RecordRequest("GET", "/api/info", 200, 25);
        monitoringService.RecordRequest("GET", "/api/info", 500, 30);

        var controller = new MonitoringController(monitoringService);

        // Act
        var result = controller.Metrics();

        // Assert
        var contentResult = Assert.IsType<ContentResult>(result);
        Assert.Equal("text/plain; version=0.0.4", contentResult.ContentType);
        Assert.Contains("bank_reporting_requests_total", contentResult.Content);
        Assert.Contains("bank_reporting_errors_total", contentResult.Content);
        Assert.Contains("route=\"GET /api/info\"", contentResult.Content);
    }

    [Fact]
    public void MonitoringService_TracksErrorCount_WhenStatusIs5xx()
    {
        // Arrange
        var monitoringService = new MonitoringService();

        // Act
        monitoringService.RecordRequest("POST", "/api/declare", 200, 20);
        monitoringService.RecordRequest("POST", "/api/declare", 503, 40);

        var metrics = monitoringService.BuildPrometheusMetrics();

        // Assert
        Assert.Contains("bank_reporting_requests_total 2", metrics);
        Assert.Contains("bank_reporting_errors_total 1", metrics);
        Assert.Contains("bank_reporting_route_errors_total{route=\"POST /api/declare\"} 1", metrics);
    }
}

public class SystemControllerTests
{
    private readonly Mock<IAgentService> _mockAgentService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly SystemController _controller;

    public SystemControllerTests()
    {
        _mockAgentService = new Mock<IAgentService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockConfiguration.Setup(x => x["AgentSettings:BaseUrl"]).Returns("https://127.0.0.1:8005/APBSA");
        _mockConfiguration.Setup(x => x["AgentSettings:AutoUpdateTime"]).Returns("03:00");
        _controller = new SystemController(_mockAgentService.Object, _mockConfiguration.Object);
    }

    [Fact]
    public async Task CheckVersion_ReturnsOk()
    {
        // Arrange
        _mockAgentService
            .Setup(x => x.CheckVersionAsync())
            .ReturnsAsync(new ApiResponse<VersionInfo>
            {
                Code = "0000",
                Msg = "檢查成功",
                Payload = new VersionInfo { Version = "1.0.0", LatestVersion = "1.0.1" }
            });

        // Act
        var result = await _controller.CheckVersion();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetInfo_ReturnsOk()
    {
        // Arrange
        _mockAgentService
            .Setup(x => x.GetAgentInfoAsync())
            .ReturnsAsync(new ApiResponse<AgentInfo>
            {
                Code = "0000",
                Msg = "查詢成功",
                Payload = new AgentInfo { Version = "1.0.0", Token = "***", Key = "已匯入" }
            });

        // Act
        var result = await _controller.GetInfo();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetSettings_ReturnsOk()
    {
        // Act
        var result = _controller.GetSettings();

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void UpdateSettings_ReturnsOk()
    {
        // Arrange
        var request = new SystemSettingsRequest
        {
            ApiServerUrl = "https://127.0.0.1:8005/APBSA",
            AutoUpdateTime = "04:00"
        };

        // Act
        var result = _controller.UpdateSettings(request);

        // Assert
        Assert.IsType<OkObjectResult>(result);
    }
}
