using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankReporting.Tests;

public class ComplianceAuditServiceTests
{
    [Fact]
    public async Task GenerateReportAsync_BuildsSummaryFromAuditTrails()
    {
        var service = new ComplianceAuditService();
        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-20),
            User = "alice",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });
        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
            User = "bob",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 500,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var report = await service.GenerateReportAsync(new ComplianceAuditReportGenerateRequest(), CancellationToken.None);

        Assert.Equal(2, report.Summary.TotalRequests);
        Assert.Equal(1, report.Summary.FailedRequests);
        Assert.Equal(2, report.Summary.SensitiveOperations);
        Assert.Equal(1, report.Summary.HighRiskOperations);
        Assert.Equal(2, report.Summary.UniqueUsers);
    }
}

public class ComplianceControllerTests
{
    [Fact]
    public async Task GenerateAuditReport_ReturnsOk()
    {
        var service = new ComplianceAuditService();
        var controller = new ComplianceController(service);

        var result = await controller.GenerateAuditReport(new ComplianceAuditReportGenerateRequest());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task QueryAuditTrails_TrimsInput()
    {
        var service = new ComplianceAuditService();
        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow,
            User = "alice",
            Path = "/api/admin/users",
            Method = "GET",
            StatusCode = 200,
            RiskLevel = "medium"
        });

        var controller = new ComplianceController(service);
        var result = controller.QueryAuditTrails(new AuditTrailQueryRequest
        {
            User = " alice ",
            Path = " /api/admin ",
            RiskLevel = " medium "
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<AuditTrailQueryPayload>>(ok.Value);
        Assert.Single(payload.Payload!.Records);
    }
}
