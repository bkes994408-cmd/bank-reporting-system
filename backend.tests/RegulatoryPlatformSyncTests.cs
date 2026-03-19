using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace BankReporting.Tests;

public class RegulatoryPlatformSyncServiceTests
{
    [Fact]
    public async Task GenerateAndSyncAuditReportAsync_ShouldReturnSuccess_WhenThirdPartySyncSuccess()
    {
        var auditService = new Mock<IComplianceAuditService>();
        auditService.Setup(x => x.GenerateReportAsync(It.IsAny<ComplianceAuditReportGenerateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ComplianceAuditReportRecord
            {
                ReportId = "audit-001",
                GeneratedAtUtc = DateTime.UtcNow,
                StartDateUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDateUtc = new DateTime(2026, 3, 31, 23, 59, 59, DateTimeKind.Utc)
            });

        var integrationService = new Mock<IThirdPartyIntegrationService>();
        integrationService.Setup(x => x.SyncAsync(It.IsAny<ThirdPartySyncRequest>()))
            .ReturnsAsync(new ApiResponse<ThirdPartySyncResult>
            {
                Code = "0000",
                Msg = "ok",
                Payload = new ThirdPartySyncResult
                {
                    SystemName = "regulator-platform",
                    Success = true,
                    StatusCode = 200
                }
            });

        var service = new RegulatoryPlatformSyncService(auditService.Object, integrationService.Object);

        var result = await service.GenerateAndSyncAuditReportAsync(new RegulatoryAuditReportSyncRequest
        {
            BankCode = "822",
            PlatformSystemName = "regulator-platform"
        }, CancellationToken.None);

        Assert.Equal("0000", result.Code);
        Assert.NotNull(result.Payload);
        Assert.True(result.Payload!.Synced);
        Assert.Equal("audit-001", result.Payload.AuditReport.ReportId);
        Assert.Equal("regulator-platform", result.Payload.SyncResult.SystemName);
    }
}

public class RegulatoryPlatformSyncControllerTests
{
    [Fact]
    public async Task SyncAuditReportToRegulatorPlatform_ShouldReturnBadRequest_WhenBankCodeMissing()
    {
        var controller = BuildController();

        var result = await controller.SyncAuditReportToRegulatorPlatform(new RegulatoryAuditReportSyncRequest());

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<object>>(bad.Value);
        Assert.Equal("COMPLIANCE_4008", response.Code);
    }

    [Fact]
    public async Task SyncAuditReportToRegulatorPlatform_ShouldReturnOk_WhenServiceSuccess()
    {
        var syncService = new Mock<IRegulatoryPlatformSyncService>();
        syncService.Setup(x => x.GenerateAndSyncAuditReportAsync(It.IsAny<RegulatoryAuditReportSyncRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResponse<RegulatoryAuditReportSyncResult>
            {
                Code = "0000",
                Msg = "ok",
                Payload = new RegulatoryAuditReportSyncResult
                {
                    BankCode = "822",
                    PlatformSystemName = "regulator-platform",
                    Synced = true,
                    AuditReport = new ComplianceAuditReportRecord { ReportId = "audit-001" },
                    SyncResult = new ThirdPartySyncResult { Success = true, SystemName = "regulator-platform" }
                }
            });

        var controller = BuildController(syncService.Object);
        var result = await controller.SyncAuditReportToRegulatorPlatform(new RegulatoryAuditReportSyncRequest
        {
            BankCode = "822",
            PlatformSystemName = "regulator-platform"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<RegulatoryAuditReportSyncResult>>(ok.Value);
        Assert.Equal("0000", payload.Code);
        Assert.True(payload.Payload!.Synced);
    }

    private static ComplianceController BuildController(IRegulatoryPlatformSyncService? syncService = null)
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new Mock<IExternalComplianceDataService>();
        var alertService = new ComplianceAlertService(auditService);
        var predictiveService = new PredictiveComplianceRiskService(auditService, regulationService);
        var blockchainService = new BlockchainComplianceService();

        return new ComplianceController(
            auditService,
            regulationService,
            externalService.Object,
            alertService,
            predictiveService,
            blockchainService,
            regulatoryPlatformSyncService: syncService);
    }
}
