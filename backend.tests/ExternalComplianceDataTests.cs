using System.Net;
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

public class ExternalComplianceDataServiceTests
{
    [Fact]
    public async Task SyncRiskDataAsync_ImportsAndNormalizesRecords()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\":\"s-1\",\"entity_name\":\"John Doe\",\"country\":\"tw\",\"severity\":\"HIGH\",\"tags\":\"sanction,pep\"}]", Encoding.UTF8, "application/json")
            }));

        var service = BuildService(handler);
        var sync = await service.SyncRiskDataAsync(new ExternalRiskDataSyncRequest
        {
            ProviderName = "kyc-aml-provider",
            DatasetType = "sanctions"
        }, CancellationToken.None);

        Assert.Equal(1, sync.ImportedCount);

        var screening = service.ScreenRisk(new ExternalRiskScreeningRequest
        {
            CustomerName = "John Doe",
            Country = "TW",
            DatasetType = "sanctions"
        });

        Assert.Equal(1, screening.TotalMatches);
        Assert.Equal("block", screening.SuggestedDecision);
        Assert.Equal("TW", screening.Matches[0].Country);
    }

    [Fact]
    public async Task SyncRiskDataAsync_WhenProviderDisabled_Throws()
    {
        var service = BuildService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncRiskDataAsync(new ExternalRiskDataSyncRequest
        {
            ProviderName = "unknown",
            DatasetType = "pep"
        }, CancellationToken.None));
    }

    private static ExternalComplianceDataService BuildService(HttpMessageHandler handler)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler));

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ExternalComplianceData:Providers:0:Name"] = "kyc-aml-provider",
            ["ExternalComplianceData:Providers:0:BaseUrl"] = "https://compliance.example.com",
            ["ExternalComplianceData:Providers:0:FetchPath"] = "/api/v1/risk-lists/sanctions",
            ["ExternalComplianceData:Providers:0:Enabled"] = "true",
            ["ExternalComplianceData:Providers:0:TimeoutSeconds"] = "15"
        }).Build();

        return new ExternalComplianceDataService(factory.Object, config);
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

public class ExternalComplianceDataControllerTests
{
    [Fact]
    public async Task SyncExternalRiskData_ReturnsBadRequest_WhenProviderMissing()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new Mock<IExternalComplianceDataService>();
        var controller = new ComplianceController(auditService, regulationService, externalService.Object);

        var result = await controller.SyncExternalRiskData(new ExternalRiskDataSyncRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ScreenExternalRisk_ReturnsBadRequest_WhenCustomerNameMissing()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new Mock<IExternalComplianceDataService>();
        var controller = new ComplianceController(auditService, regulationService, externalService.Object);

        var result = controller.ScreenExternalRisk(new ExternalRiskScreeningRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }
}

public sealed class StubExternalComplianceDataService : IExternalComplianceDataService
{
    public Task<ExternalRiskDataSyncResult> SyncRiskDataAsync(ExternalRiskDataSyncRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new ExternalRiskDataSyncResult
        {
            ProviderName = request.ProviderName,
            DatasetType = request.DatasetType,
            ImportedCount = 0,
            SkippedCount = 0,
            SyncedAtUtc = DateTime.UtcNow
        });

    public ExternalRiskScreeningResult ScreenRisk(ExternalRiskScreeningRequest request)
        => new()
        {
            CustomerName = request.CustomerName,
            Country = request.Country,
            SuggestedDecision = "clear"
        };
}
