using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankReporting.Tests;

public class ComplianceAlertServiceTests
{
    [Fact]
    public void Evaluate_TriggersFailedRequestAlert_WithConfiguredRule()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "test-failed-requests",
            Name = "失敗請求測試",
            RuleType = "failed_requests",
            Enabled = true,
            Severity = "high",
            Threshold = 3,
            WindowMinutes = 15
        });

        for (var i = 0; i < 3; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
                User = "alice",
                Method = "POST",
                Path = "/api/declare",
                StatusCode = 500,
                IsSensitiveOperation = true,
                RiskLevel = "high"
            });
        }

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest
        {
            NotifyChannels = new List<string> { "in-app", "email" }
        });

        Assert.True(result.TriggeredAlerts >= 1);
        Assert.Contains(result.Alerts, x => x.RuleId == "test-failed-requests");
        Assert.Contains(result.Alerts.SelectMany(x => x.NotifyChannels), x => x == "email");
    }

    [Fact]
    public void Evaluate_UsesWindowMinutesOverride_WhenProvided()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "window-override",
            Name = "Window override",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 1,
            SensitiveOnly = false
        });

        // 在規則預設 window=1 分鐘之外，但在 override=5 分鐘之內
        for (var i = 0; i < 2; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-3),
                User = "bob",
                Method = "POST",
                Path = "/api/declare",
                StatusCode = 500,
                IsSensitiveOperation = false,
                RiskLevel = "medium"
            });
        }

        var noOverride = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.DoesNotContain(noOverride.Alerts, x => x.RuleId == "window-override");

        var withOverride = service.Evaluate(new ComplianceAlertEvaluateRequest { WindowMinutes = 5 });
        var alert = Assert.Single(withOverride.Alerts.Where(x => x.RuleId == "window-override"));
        Assert.Equal(5, alert.WindowMinutes);
    }

    [Fact]
    public void Evaluate_AppliesSensitiveOnly_ForFailedRequests()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-only-failed",
            Name = "Sensitive only failed",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 10,
            SensitiveOnly = true
        });

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "carol",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 500,
            IsSensitiveOperation = false,
            RiskLevel = "high"
        });

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "carol",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 500,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.DoesNotContain(result.Alerts, x => x.RuleId == "sensitive-only-failed");

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "carol",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 500,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var second = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.Contains(second.Alerts, x => x.RuleId == "sensitive-only-failed");
    }

    [Fact]
    public void Evaluate_ReturnsTopSubjects_WhenMultipleUsersTriggered()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "multi-subject",
            Name = "Multi subject",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 15
        });

        AddFailed(auditService, "alice", 6);
        AddFailed(auditService, "bob", 4);
        AddFailed(auditService, "chris", 3);

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest { TopSubjects = 2 });
        var alert = Assert.Single(result.Alerts.Where(x => x.RuleId == "multi-subject"));

        Assert.Equal(2, alert.TopSubjects.Count);
        Assert.Equal("alice", alert.TopSubjects[0].Subject);
        Assert.Equal("bob", alert.TopSubjects[1].Subject);
    }

    [Fact]
    public void Evaluate_PaginatesAuditTrailQuery_Beyond500Records()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "paging-rule",
            Name = "Paging rule",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 550,
            WindowMinutes = 30
        });

        AddFailed(auditService, "david", 650);

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest());
        var alert = Assert.Single(result.Alerts.Where(x => x.RuleId == "paging-rule"));
        Assert.Equal(650, alert.TriggerCount);
    }

    private static void AddFailed(ComplianceAuditService auditService, string user, int count)
    {
        for (var i = 0; i < count; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddSeconds(-i),
                User = user,
                Method = "POST",
                Path = i % 2 == 0 ? "/api/declare" : "/api/keys/import",
                StatusCode = 500,
                IsSensitiveOperation = true,
                RiskLevel = "high"
            });
        }
    }
}

public class ComplianceAlertsControllerTests
{
    [Fact]
    public void EvaluateAlerts_ReturnsOkAndPayload()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "ops",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 503,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var controller = new ComplianceController(auditService, regulationService, externalService, alertService);
        var result = controller.EvaluateAlerts(new ComplianceAlertEvaluateRequest());

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<ComplianceAlertEvaluateResult>>(ok.Value);
        Assert.Equal("0000", payload.Code);
    }

    [Fact]
    public void UpsertAlertRule_TrimsFields()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService);

        var result = controller.UpsertAlertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "  custom-rule  ",
            Name = "  自訂規則  ",
            RuleType = " failed_requests ",
            Severity = " high ",
            Threshold = 2,
            WindowMinutes = 10
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<ComplianceAlertRule>>(ok.Value);
        Assert.Equal("custom-rule", payload.Payload!.RuleId);
        Assert.Equal("自訂規則", payload.Payload.Name);
    }
}
