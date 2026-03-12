using System.Security.Cryptography;
using System.Text;
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
    public void CommitAuditAnchor_WithProvidedPayloadHash_UsesOriginalHashWithoutRehash()
    {
        var service = new BlockchainComplianceService();
        const string provided = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";

        var record = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            Network = "sandbox-ledger",
            Summary = "checkpoint",
            PayloadHash = provided
        });

        Assert.Equal(provided.ToLowerInvariant(), record.PayloadHash);
    }

    [Fact]
    public void CommitAuditAnchor_WithInvalidPayloadHash_ThrowsArgumentException()
    {
        var service = new BlockchainComplianceService();

        Assert.Throws<ArgumentException>(() => service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            PayloadHash = "not-a-valid-hash"
        }));
    }

    [Fact]
    public void QueryAuditAnchors_AppliesPagingBoundariesAndTimeRangeFilter()
    {
        var service = new BlockchainComplianceService();
        var baseline = DateTime.UtcNow;

        service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest { Network = "n1", Summary = "A" });
        service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest { Network = "n1", Summary = "B" });

        var empty = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            Network = "n1",
            FromCreatedAtUtc = baseline.AddDays(1),
            ToCreatedAtUtc = baseline.AddDays(2),
            Page = 1,
            PageSize = 20
        });

        Assert.Equal(0, empty.Total);
        Assert.Empty(empty.Records);

        var result = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            Network = "n1",
            FromCreatedAtUtc = baseline.AddMinutes(-1),
            ToCreatedAtUtc = baseline.AddMinutes(1),
            Page = 0,
            PageSize = 999
        });

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(200, result.PageSize);
        Assert.Equal(2, result.Records.Count);
    }

    [Fact]
    public async Task CommitAuditAnchor_ConcurrentCalls_ProducesSingleConsistentChain()
    {
        var service = new BlockchainComplianceService();
        const int count = 80;

        await Task.WhenAll(Enumerable.Range(1, count)
            .Select(i => Task.Run(() => service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
            {
                Network = "concurrent-net",
                Summary = $"item-{i}",
                AuditTrailIds = new List<string> { $"t-{i}" }
            }))));

        var records = service.QueryAuditAnchors(new BlockchainAuditAnchorQueryRequest
        {
            Network = "concurrent-net",
            Page = 1,
            PageSize = 200
        }).Records;

        Assert.Equal(count, records.Count);

        var byHash = records.ToDictionary(x => x.AnchorHash, StringComparer.OrdinalIgnoreCase);
        var referenced = new HashSet<string>(
            records.Where(r => !string.IsNullOrWhiteSpace(r.PreviousAnchorHash)).Select(r => r.PreviousAnchorHash!),
            StringComparer.OrdinalIgnoreCase);

        var tipCandidates = records.Where(r => !referenced.Contains(r.AnchorHash)).ToList();
        Assert.Single(tipCandidates);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = tipCandidates[0];
        while (current is not null)
        {
            Assert.True(visited.Add(current.AnchorHash), "Chain should not contain cycles");

            if (string.IsNullOrWhiteSpace(current.PreviousAnchorHash))
            {
                break;
            }

            Assert.True(byHash.TryGetValue(current.PreviousAnchorHash, out var previous), "Every previous hash must exist in chain");
            current = previous;
        }

        Assert.Equal(count, visited.Count);
    }

    [Fact]
    public void CommitAuditAnchor_NormalizesMetadataWhitespaceAndCase()
    {
        var service = new BlockchainComplianceService();

        var record = service.CommitAuditAnchor(new BlockchainAuditAnchorCommitRequest
        {
            Metadata = new Dictionary<string, string>
            {
                ["  RiskLevel  "] = "  High  ",
                ["risklevel"] = " medium ",
                ["   "] = "ignored",
                ["owner"] = "   "
            }
        });

        Assert.Single(record.Metadata);
        Assert.True(record.Metadata.ContainsKey("risklevel"));
        Assert.Equal("medium", record.Metadata["risklevel"]);
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
