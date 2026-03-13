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

    [Fact]
    public async Task SyncRiskDataAsync_WhenHttpFailure_ThrowsHttpRequestException()
    {
        var service = BuildService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("upstream error", Encoding.UTF8, "text/plain")
            })));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.SyncRiskDataAsync(new ExternalRiskDataSyncRequest
        {
            ProviderName = "kyc-aml-provider",
            DatasetType = "sanctions"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task SyncRiskDataAsync_WhenPayloadIsInvalidJson_ThrowsJsonException()
    {
        var service = BuildService(new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not-json", Encoding.UTF8, "application/json")
            })));

        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => service.SyncRiskDataAsync(new ExternalRiskDataSyncRequest
        {
            ProviderName = "kyc-aml-provider",
            DatasetType = "sanctions"
        }, CancellationToken.None));
    }

    [Fact]
    public async Task SyncRiskDataAsync_WhenRequestCancelled_ThrowsTaskCanceledException()
    {
        var service = BuildService(new StubHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[]", Encoding.UTF8, "application/json")
            };
        }));

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(80));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SyncRiskDataAsync(new ExternalRiskDataSyncRequest
        {
            ProviderName = "kyc-aml-provider",
            DatasetType = "sanctions"
        }, cts.Token));
    }

    [Fact]
    public async Task SyncRiskDataAsync_FieldMappings_AreCaseInsensitive()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"entity_name\":\"Jane Roe\",\"country\":\"us\",\"severity\":\"low\"}]", Encoding.UTF8, "application/json")
            }));

        var service = BuildService(handler);
        var result = await service.SyncRiskDataAsync(new ExternalRiskDataSyncRequest
        {
            ProviderName = "kyc-aml-provider",
            DatasetType = "sanctions",
            FieldMappings = new Dictionary<string, string>
            {
                ["NAME"] = "entity_name",
                ["Country"] = "country",
                ["RISKLEVEL"] = "severity"
            }
        }, CancellationToken.None);

        Assert.Equal(1, result.ImportedCount);

        var screening = service.ScreenRisk(new ExternalRiskScreeningRequest
        {
            CustomerName = "Jane Roe",
            DatasetType = "sanctions"
        });

        Assert.Equal(1, screening.TotalMatches);
        Assert.Equal("US", screening.Matches[0].Country);
        Assert.Equal("low", screening.Matches[0].RiskLevel);
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
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
            : this((request, _) => handler(request))
        {
        }

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
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
        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, externalService.Object, alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());

        var result = await controller.SyncExternalRiskData(new ExternalRiskDataSyncRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ScreenExternalRisk_ReturnsBadRequest_WhenCustomerNameMissing()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new Mock<IExternalComplianceDataService>();
        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, externalService.Object, alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());

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
