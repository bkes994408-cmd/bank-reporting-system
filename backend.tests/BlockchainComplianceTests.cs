using System.Collections.Concurrent;
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
    public void CommitAuditAnchor_UsesProvidedPayloadHashWithoutRehashing()
    {
        var service = new BlockchainComplianceService();
        var providedPayloadHash = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";

        var result = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            PayloadHash = providedPayloadHash,
            Summary = "should not rehash"
        });

        Assert.Equal(providedPayloadHash.ToLowerInvariant(), result.PayloadHash);
    }

    [Fact]
    public void CommitAuditAnchor_NormalizesMetadataTrimAndCase()
    {
        var service = new BlockchainComplianceService();

        var result = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            Summary = "metadata normalization",
            Metadata = new Dictionary<string, string>
            {
                ["  RiskLevel  "] = "  HIGH ",
                ["risklevel"] = " low ",
                ["  Source "] = "  AML  ",
                ["   "] = "ignored",
                ["emptyValue"] = "   "
            }
        });

        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal("low", result.Metadata["risklevel"]);
        Assert.Equal("AML", result.Metadata["source"]);
    }

    [Fact]
    public void QueryAuditAnchors_AppliesPaginationBoundariesAndEmptyResult()
    {
        var service = new BlockchainComplianceService();
        for (var i = 0; i < 3; i++)
        {
            service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
            {
                Network = "sandbox-ledger",
                Summary = $"record-{i}"
            });
        }

        var boundary = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            Page = 0,
            PageSize = 500
        });

        Assert.Equal(1, boundary.Page);
        Assert.Equal(200, boundary.PageSize);
        Assert.Equal(3, boundary.Total);
        Assert.Equal(3, boundary.Records.Count);

        var empty = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            Network = "does-not-exist"
        });

        Assert.Equal(0, empty.Total);
        Assert.Empty(empty.Records);
    }

    [Fact]
    public void QueryAuditAnchors_FiltersByTimeRange()
    {
        var service = new BlockchainComplianceService();
        var first = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest { Summary = "first" });
        Thread.Sleep(20);
        var second = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest { Summary = "second" });

        var result = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            FromCreatedAtUtc = second.CreatedAtUtc.AddMilliseconds(-1),
            ToCreatedAtUtc = second.CreatedAtUtc.AddMilliseconds(1)
        });

        Assert.Single(result.Records);
        Assert.Equal(second.AnchorId, result.Records[0].AnchorId);
        Assert.DoesNotContain(result.Records, x => x.AnchorId == first.AnchorId);
    }

    [Fact]
    public void CommitAuditAnchor_IsChainConsistentUnderConcurrency()
    {
        var service = new BlockchainComplianceService();
        const int total = 80;

        Parallel.For(0, total, i =>
        {
            service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
            {
                Network = "concurrent-net",
                Summary = $"parallel-{i}"
            });
        });

        var result = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            Network = "concurrent-net",
            Page = 1,
            PageSize = 200
        });

        Assert.Equal(total, result.Total);

        var records = result.Records;
        Assert.Equal(total, records.Count);

        var genesisCount = records.Count(x => x.PreviousAnchorHash is null);
        Assert.Equal(1, genesisCount);

        var previousGroups = records
            .Where(x => !string.IsNullOrWhiteSpace(x.PreviousAnchorHash))
            .GroupBy(x => x.PreviousAnchorHash!, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.All(previousGroups, g => Assert.Single(g));

        var hashes = records.Select(x => x.AnchorHash).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(records.Where(x => !string.IsNullOrWhiteSpace(x.PreviousAnchorHash)), x =>
        {
            Assert.Contains(x.PreviousAnchorHash!, hashes);
        });
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
    public void CommitBlockchainAnchor_InvalidPayloadHash_ReturnsBadRequest()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);
        var blockchainService = new BlockchainComplianceService();
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, blockchainService);

        var result = controller.CommitBlockchainAnchor(new BlockchainAuditAnchorCommitRequest
        {
            PayloadHash = "not-a-sha256"
        });

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<object>>(bad.Value);
        Assert.Equal("COMPLIANCE_4006", payload.Code);
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
