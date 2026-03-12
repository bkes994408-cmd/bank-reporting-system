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
    public void Evaluate_AppliesSensitiveOnly_ForHighRiskOperations()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-only-high-risk",
            Name = "Sensitive only high risk",
            RuleType = "high_risk_operations",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 10,
            RiskLevel = "high",
            SensitiveOnly = true
        });

        // 同為 high risk，但僅 1 筆敏感操作，應不觸發
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "dora",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 200,
            IsSensitiveOperation = false,
            RiskLevel = "high"
        });
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "dora",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var first = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.DoesNotContain(first.Alerts, x => x.RuleId == "sensitive-only-high-risk");

        // 第二筆敏感操作加入後才應觸發
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "dora",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var second = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.Contains(second.Alerts, x => x.RuleId == "sensitive-only-high-risk");
    }

    [Fact]
    public void Evaluate_AppliesSensitiveOnly_ForOffHoursSensitive()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-only-off-hours",
            Name = "Sensitive only off-hours",
            RuleType = "off_hours_sensitive",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 120,
            SensitiveOnly = true
        });

        var offHoursUtc = DateTime.UtcNow.Date.AddHours(2);
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = offHoursUtc,
            User = "eric",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = false,
            RiskLevel = "medium"
        });
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = offHoursUtc.AddMinutes(-1),
            User = "eric",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });

        var first = service.Evaluate(new ComplianceAlertEvaluateRequest { WindowMinutes = 1440 });
        Assert.DoesNotContain(first.Alerts, x => x.RuleId == "sensitive-only-off-hours");

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = offHoursUtc.AddMinutes(-2),
            User = "eric",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });

        var second = service.Evaluate(new ComplianceAlertEvaluateRequest { WindowMinutes = 1440 });
        Assert.Contains(second.Alerts, x => x.RuleId == "sensitive-only-off-hours");
    }

    [Fact]
    public void Evaluate_HighRiskSensitiveOnly_IgnoresSensitiveButNonHighRiskRecords()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-only-high-risk-level-filter",
            Name = "Sensitive only high risk with level filter",
            RuleType = "high_risk_operations",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 10,
            RiskLevel = "high",
            SensitiveOnly = true
        });

        // 兩筆敏感但非 high 風險，應被 high_risk_operations 規則排除
        for (var i = 0; i < 2; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
                User = "dora",
                Method = "POST",
                Path = "/api/keys/import",
                StatusCode = 200,
                IsSensitiveOperation = true,
                RiskLevel = "medium"
            });
        }

        // 只有 1 筆同時滿足 sensitive + high risk，未達 threshold 不應觸發
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "dora",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var first = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.DoesNotContain(first.Alerts, x => x.RuleId == "sensitive-only-high-risk-level-filter");

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "dora",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var second = service.Evaluate(new ComplianceAlertEvaluateRequest());
        var alert = Assert.Single(second.Alerts.Where(x => x.RuleId == "sensitive-only-high-risk-level-filter"));
        Assert.Equal(2, alert.TriggerCount);
    }

    [Fact]
    public void Evaluate_OffHoursSensitiveOnly_IgnoresInHoursSensitiveRecords()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-only-off-hours-time-filter",
            Name = "Sensitive only off-hours with time filter",
            RuleType = "off_hours_sensitive",
            Enabled = true,
            Threshold = 2,
            WindowMinutes = 1440,
            SensitiveOnly = true
        });

        var dayTimeUtc = DateTime.UtcNow.Date.AddHours(10);
        var offHoursUtc = DateTime.UtcNow.Date.AddHours(2);

        // 兩筆敏感但在上班時段，off_hours_sensitive 應排除
        for (var i = 0; i < 2; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = dayTimeUtc.AddMinutes(i),
                User = "eric",
                Method = "POST",
                Path = "/api/declare",
                StatusCode = 200,
                IsSensitiveOperation = true,
                RiskLevel = "medium"
            });
        }

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = offHoursUtc,
            User = "eric",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });

        var first = service.Evaluate(new ComplianceAlertEvaluateRequest { WindowMinutes = 1440 });
        Assert.DoesNotContain(first.Alerts, x => x.RuleId == "sensitive-only-off-hours-time-filter");

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = offHoursUtc.AddMinutes(-1),
            User = "eric",
            Method = "POST",
            Path = "/api/declare",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "medium"
        });

        var second = service.Evaluate(new ComplianceAlertEvaluateRequest { WindowMinutes = 1440 });
        var alert = Assert.Single(second.Alerts.Where(x => x.RuleId == "sensitive-only-off-hours-time-filter"));
        Assert.Equal(2, alert.TriggerCount);
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


    [Fact]
    public void Evaluate_PagingBoundary_Exactly500Records_StillTriggers()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "paging-500",
            Name = "Paging 500",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 500,
            WindowMinutes = 30
        });

        AddFailed(auditService, "frank", 500);

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest());
        var alert = Assert.Single(result.Alerts.Where(x => x.RuleId == "paging-500"));
        Assert.Equal(500, alert.TriggerCount);
    }

    [Fact]
    public void Evaluate_PagingBoundary_Exactly1000Records_AggregatesTwoFullPages()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "paging-1000",
            Name = "Paging 1000",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 1000,
            WindowMinutes = 30
        });

        AddFailed(auditService, "gina", 1000);

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest());
        var alert = Assert.Single(result.Alerts.Where(x => x.RuleId == "paging-1000"));
        Assert.Equal(1000, alert.TriggerCount);
    }

    [Fact]
    public void Evaluate_PagingBoundary_1001Records_IncludesTrailingPartialPage()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "paging-1001",
            Name = "Paging 1001",
            RuleType = "failed_requests",
            Enabled = true,
            Threshold = 1001,
            WindowMinutes = 30
        });

        AddFailed(auditService, "henry", 1001);

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest());
        var alert = Assert.Single(result.Alerts.Where(x => x.RuleId == "paging-1001"));
        Assert.Equal(1001, alert.TriggerCount);
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
