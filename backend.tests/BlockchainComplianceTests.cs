using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankReporting.Tests;

public class BlockchainComplianceServiceTests
{
    [Fact]
    public void CommitAuditAnchor_CreatesHashChainedRecords()
    {
        var service = new BlockchainComplianceService();

        var first = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            Network = "sandbox-ledger",
            Summary = "first checkpoint",
            AuditTrailIds = new List<string> { "t-1", "t-2" }
        });

        var second = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            Network = "sandbox-ledger",
            Summary = "second checkpoint",
            AuditTrailIds = new List<string> { "t-3" }
        });

        Assert.NotEmpty(first.AnchorHash);
        Assert.Equal(first.AnchorHash, second.PreviousAnchorHash);
    }

    [Fact]
    public void SimulateDataSharing_FlagsPotentialPIIFields()
    {
        var service = new BlockchainComplianceService();

        var result = service.SimulateDataSharing(new BlockchainDataSharingSimulationRequest
        {
            SourceInstitution = "Bank-A",
            TargetInstitution = "Bank-B",
            Purpose = "cross-bank-raw-share",
            Fields = new List<string> { "customerName", "customerId", "riskScore" }
        });

        Assert.NotEmpty(result.PolicyViolations);
        Assert.Equal("zk-proof-or-aggregated-metrics", result.RecommendedMode);
    }
}

public class BlockchainComplianceControllerTests
{
    [Fact]
    public void CommitBlockchainAnchor_ReturnsOkWithPayload()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);
        var blockchainService = new BlockchainComplianceService();
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, blockchainService);

        var result = controller.CommitBlockchainAnchor(new BlockchainAuditAnchorCommitRequest
        {
            AnchorType = " audit_trail ",
            Network = " sandbox-ledger ",
            Summary = "  nightly checkpoint  "
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<BlockchainAuditAnchorRecord>>(ok.Value);
        Assert.Equal("0000", payload.Code);
        Assert.Equal("sandbox-ledger", payload.Payload!.Network);
        Assert.Equal("nightly checkpoint", payload.Payload.Summary);
    }

    [Fact]
    public void SimulateBlockchainDataSharing_MissingInstitution_ReturnsBadRequest()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);
        var blockchainService = new BlockchainComplianceService();
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, blockchainService);

        var result = controller.SimulateBlockchainDataSharing(new BlockchainDataSharingSimulationRequest
        {
            SourceInstitution = "",
            TargetInstitution = "Bank-B"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<object>>(badRequest.Value);
        Assert.Equal("COMPLIANCE_4005", payload.Code);
    }
}
