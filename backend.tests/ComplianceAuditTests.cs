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

public class RegulationMonitoringServiceTests
{
    [Fact]
    public async Task AnalyzeLatestAsync_DetectsChangesAndImpactAreas()
    {
        var service = new RegulationMonitoringService();
        service.UpsertSnapshot(new RegulationSnapshotUpsertRequest
        {
            Source = "FSC",
            DocumentCode = "AML-001",
            Title = "防制洗錢辦法",
            Content = "第一條 申報期限為次月10日。\n第二條 報表欄位包含客戶名稱。",
            PublishedAtUtc = DateTime.UtcNow.AddDays(-10)
        });
        service.UpsertSnapshot(new RegulationSnapshotUpsertRequest
        {
            Source = "FSC",
            DocumentCode = "AML-001",
            Title = "防制洗錢辦法",
            Content = "第一條 申報期限為次月5日。\n第二條 報表欄位包含客戶名稱與交易對象。\n第三條 資料留存期限至少五年。",
            PublishedAtUtc = DateTime.UtcNow.AddDays(-1)
        });

        var result = await service.AnalyzeLatestAsync(new RegulationImpactAnalysisRequest
        {
            Source = "FSC",
            DocumentCode = "AML-001"
        }, CancellationToken.None);

        Assert.NotEmpty(result.Changes);
        Assert.Contains(result.ImpactAreas, x => x.Domain == "申報流程");
        Assert.Contains(result.ImpactAreas, x => x.Domain == "報表格式");
        Assert.Contains(result.ImpactAreas, x => x.Domain == "數據採集");
        Assert.Contains(result.RecommendedActions, x => x.Contains("排程"));
    }
}

public class ComplianceControllerTests
{
    [Fact]
    public async Task GenerateAuditReport_ReturnsOk()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService());

        var result = await controller.GenerateAuditReport(new ComplianceAuditReportGenerateRequest());

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void QueryAuditTrails_TrimsInput()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow,
            User = "alice",
            Path = "/api/admin/users",
            Method = "GET",
            StatusCode = 200,
            RiskLevel = "medium"
        });

        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService());
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

    [Fact]
    public async Task GenerateRegulationImpactAnalysis_ReturnsBadRequest_WhenNoBaseline()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        regulationService.UpsertSnapshot(new RegulationSnapshotUpsertRequest
        {
            Source = "FSC",
            DocumentCode = "AML-002",
            Title = "test",
            Content = "第一條 測試"
        });

        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService());
        var result = await controller.GenerateRegulationImpactAnalysis(new RegulationImpactAnalysisRequest
        {
            Source = "FSC",
            DocumentCode = "AML-002"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
