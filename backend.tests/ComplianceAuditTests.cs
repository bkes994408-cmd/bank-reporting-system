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
        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService(), alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());

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
            RiskLevel = "medium",
            IsSensitiveOperation = true,
            DurationMs = 1200
        });

        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService(), alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());
        var result = controller.QueryAuditTrails(new AuditTrailQueryRequest
        {
            User = " alice ",
            Path = " /api/admin ",
            RiskLevel = " medium ",
            SensitiveOnly = true,
            MinStatusCode = 200,
            MaxStatusCode = 299,
            MinDurationMs = 1000
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<AuditTrailQueryPayload>>(ok.Value);
        Assert.Single(payload.Payload!.Records);
    }

    [Fact]
    public void GetAuditBehaviorInsights_ReturnsTopUsersAndSuggestions()
    {
        var auditService = new ComplianceAuditService();
        var now = DateTime.UtcNow;
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-3),
            User = "alice",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 500,
            DurationMs = 2500,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-2),
            User = "alice",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            DurationMs = 2200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-1),
            User = "bob",
            Method = "GET",
            Path = "/api/reports",
            StatusCode = 200,
            DurationMs = 100,
            IsSensitiveOperation = false,
            RiskLevel = "low"
        });

        var payload = auditService.GetBehaviorInsights(new AuditBehaviorInsightsRequest
        {
            StartDateUtc = now.AddHours(-1),
            EndDateUtc = now,
            TopUsers = 2,
            TopPaths = 2
        });

        Assert.Equal(3, payload.TotalRecords);
        Assert.Equal("alice", payload.TopActiveUsers.First().User);
        Assert.NotEmpty(payload.OptimizationSuggestions);
    }

    [Fact]
    public void QueryAuditTrailTrace_ReturnsOrderedStepsByTraceId()
    {
        var auditService = new ComplianceAuditService();
        var now = DateTime.UtcNow;
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-5),
            TraceId = "trace-001",
            User = "alice",
            Method = "GET",
            Path = "/api/reports",
            StatusCode = 200,
            DurationMs = 120
        });
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-4),
            TraceId = "trace-001",
            User = "alice",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 202,
            DurationMs = 340
        });

        var payload = auditService.QueryTrace(new AuditTrailTraceRequest
        {
            TraceId = "trace-001",
            MaxSteps = 10
        });

        Assert.Equal(2, payload.TotalSteps);
        Assert.True(payload.Steps[0].TimestampUtc <= payload.Steps[1].TimestampUtc);
        Assert.All(payload.Steps, step => Assert.Equal("trace-001", step.TraceId));
    }

    [Fact]
    public void QueryAuditTrailTrace_TrimsInput()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var now = DateTime.UtcNow;
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now,
            TraceId = "trace-abc",
            User = "alice",
            Method = "GET",
            Path = "/api/reports",
            StatusCode = 200
        });

        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService(), alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());
        var result = controller.QueryAuditTrailTrace(new AuditTrailTraceRequest
        {
            TraceId = " trace-abc ",
            User = " alice ",
            MaxSteps = 10
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<AuditTrailTracePayload>>(ok.Value);
        Assert.Single(payload.Payload!.Steps);
    }

    [Fact]
    public async Task GenerateAuditReport_SwapsInvalidTimeRangeAndStillReturnsData()
    {
        var auditService = new ComplianceAuditService();
        var now = DateTime.UtcNow;
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-5),
            User = "alice",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });

        var report = await auditService.GenerateReportAsync(new ComplianceAuditReportGenerateRequest
        {
            StartDateUtc = now,
            EndDateUtc = now.AddHours(-1)
        }, CancellationToken.None);

        Assert.Equal(1, report.Summary.TotalRequests);
    }

    [Fact]
    public void QueryAuditTrails_SwapsInvalidTimeRangeAndFiltersCorrectly()
    {
        var auditService = new ComplianceAuditService();
        var now = DateTime.UtcNow;

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-10),
            User = "alice",
            Method = "GET",
            Path = "/api/reports",
            StatusCode = 200,
            DurationMs = 100
        });

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = now.AddMinutes(-120),
            User = "bob",
            Method = "GET",
            Path = "/api/reports",
            StatusCode = 200,
            DurationMs = 100
        });

        var payload = auditService.QueryTrails(new AuditTrailQueryRequest
        {
            StartDateUtc = now,
            EndDateUtc = now.AddHours(-1)
        });

        Assert.Single(payload.Records);
        Assert.Equal("alice", payload.Records[0].User);
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

        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService(), alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());
        var result = await controller.GenerateRegulationImpactAnalysis(new RegulationImpactAnalysisRequest
        {
            Source = "FSC",
            DocumentCode = "AML-002"
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void CheckDataIntegrity_DetectsInvalidTrailRecords()
    {
        var service = new ComplianceAuditService();
        service.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow,
            TraceId = "trace-integrity",
            User = "alice",
            Method = "",
            Path = "/api/reports",
            StatusCode = 999,
            DurationMs = -1
        });

        var payload = service.CheckDataIntegrity(new DataIntegrityCheckRequest { MaxIssues = 10 });

        Assert.False(payload.IsConsistent);
        Assert.True(payload.IssueCount >= 2);
        Assert.Contains(payload.Issues, x => x.Type == "trail_required_field_missing");
        Assert.Contains(payload.Issues, x => x.Type == "trail_status_code_invalid");
    }

    [Fact]
    public void RunAuditTrailIntegrityCheck_ReturnsOkPayload()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, new StubExternalComplianceDataService(), alertService, new PredictiveComplianceRiskService(auditService, regulationService), new BlockchainComplianceService());

        var result = controller.RunAuditTrailIntegrityCheck(new DataIntegrityCheckRequest { MaxIssues = 20 });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<AuditDataIntegrityPayload>>(ok.Value);
        Assert.NotNull(payload.Payload);
        Assert.Equal("0000", payload.Code);
    }
}
