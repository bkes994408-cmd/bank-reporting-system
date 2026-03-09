using System.Net;
using System.Net.Http;
using System.Text;
using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BankReporting.Tests;

public class ThirdPartyIntegrationsControllerTests
{
    [Fact]
    public void GetSystems_ReturnsOk()
    {
        var mock = new Mock<IThirdPartyIntegrationService>();
        mock.Setup(x => x.GetEnabledSystems()).Returns(new List<string> { "accounting" });
        var controller = new ThirdPartyIntegrationsController(mock.Object);

        var result = controller.GetSystems();

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<ThirdPartySystemsPayload>>(ok.Value);
        Assert.Equal("0000", payload.Code);
        Assert.Contains("accounting", payload.Payload!.Systems);
    }

    [Fact]
    public async Task Sync_WhenMissingField_ReturnsBadRequest()
    {
        var mock = new Mock<IThirdPartyIntegrationService>();
        var controller = new ThirdPartyIntegrationsController(mock.Object);

        var result = await controller.Sync(new ThirdPartySyncRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Sync_WhenSystemNotFound_ReturnsNotFound()
    {
        var mock = new Mock<IThirdPartyIntegrationService>();
        mock.Setup(x => x.SyncAsync(It.IsAny<ThirdPartySyncRequest>()))
            .ReturnsAsync(new ApiResponse<ThirdPartySyncResult> { Code = "4040", Msg = "not found" });
        var controller = new ThirdPartyIntegrationsController(mock.Object);

        var result = await controller.Sync(new ThirdPartySyncRequest
        {
            SystemName = "erp",
            EventType = "report.declaration",
            BankCode = "0070000",
            ReportId = "AI330",
            Period = "2026-03",
            Status = "success"
        });

        Assert.IsType<NotFoundObjectResult>(result);
    }
}

public class ThirdPartyIntegrationServiceTests
{
    [Fact]
    public async Task SyncAsync_WhenTargetEnabledAndReturns200_ReturnsSuccess()
    {
        HttpRequestMessage? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain")
            });
        });

        var service = BuildService(handler);
        var result = await service.SyncAsync(new ThirdPartySyncRequest
        {
            SystemName = "accounting",
            EventType = "report.declaration",
            BankCode = "0070000",
            ReportId = "AI330",
            Period = "2026-03",
            Status = "success"
        });

        Assert.Equal("0000", result.Code);
        Assert.NotNull(captured);
        Assert.Equal("https://accounting.example.com/api/v1/reporting/sync", captured!.RequestUri!.ToString());
    }

    [Fact]
    public async Task SyncAsync_WhenSystemDisabled_Returns4040()
    {
        var service = BuildService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var result = await service.SyncAsync(new ThirdPartySyncRequest
        {
            SystemName = "erp",
            EventType = "report.declaration",
            BankCode = "0070000",
            ReportId = "AI330",
            Period = "2026-03",
            Status = "success"
        });

        Assert.Equal("4040", result.Code);
    }

    private static ThirdPartyIntegrationService BuildService(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ThirdPartyIntegrations:Systems:0:Name"] = "accounting",
            ["ThirdPartyIntegrations:Systems:0:BaseUrl"] = "https://accounting.example.com",
            ["ThirdPartyIntegrations:Systems:0:SyncPath"] = "/api/v1/reporting/sync",
            ["ThirdPartyIntegrations:Systems:0:Enabled"] = "true",
            ["ThirdPartyIntegrations:Systems:1:Name"] = "erp",
            ["ThirdPartyIntegrations:Systems:1:BaseUrl"] = "https://erp.example.com",
            ["ThirdPartyIntegrations:Systems:1:SyncPath"] = "/api/v1/compliance/reporting",
            ["ThirdPartyIntegrations:Systems:1:Enabled"] = "false"
        }).Build();

        return new ThirdPartyIntegrationService(factory.Object, config);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
