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
    public void Evaluate_FailedRequestsSensitiveOnly_IgnoresNonSensitiveRecords()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-failed-requests",
            Name = "僅敏感失敗請求",
            RuleType = "failed_requests",
            Enabled = true,
            Severity = "high",
            Threshold = 2,
            WindowMinutes = 30,
            SensitiveOnly = true
        });

        for (var i = 0; i < 3; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-2),
                User = "bob",
                Method = "POST",
                Path = "/api/declare",
                StatusCode = 500,
                IsSensitiveOperation = false,
                RiskLevel = "high"
            });
        }

        var result = service.Evaluate(new ComplianceAlertEvaluateRequest());

        Assert.DoesNotContain(result.Alerts, x => x.RuleId == "sensitive-failed-requests");
    }

    [Fact]
    public void Evaluate_HighRiskSensitiveOnly_TriggersOnlyFromSensitiveRecords()
    {
        var auditService = new ComplianceAuditService();
        var service = new ComplianceAlertService(auditService);

        service.UpsertRule(new ComplianceAlertRuleUpsertRequest
        {
            RuleId = "sensitive-high-risk",
            Name = "僅敏感高風險操作",
            RuleType = "high_risk_operations",
            Enabled = true,
            Severity = "critical",
            Threshold = 2,
            WindowMinutes = 30,
            RiskLevel = "high",
            SensitiveOnly = true
        });

        for (var i = 0; i < 2; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-2),
                User = "carol",
                Method = "POST",
                Path = "/api/transfer",
                StatusCode = 200,
                IsSensitiveOperation = false,
                RiskLevel = "high"
            });
        }

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "carol",
            Method = "POST",
            Path = "/api/transfer",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var withoutThreshold = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.DoesNotContain(withoutThreshold.Alerts, x => x.RuleId == "sensitive-high-risk");

        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-1),
            User = "carol",
            Method = "POST",
            Path = "/api/transfer",
            StatusCode = 200,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var withThreshold = service.Evaluate(new ComplianceAlertEvaluateRequest());
        Assert.Contains(withThreshold.Alerts, x => x.RuleId == "sensitive-high-risk");
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

        var blockchainService = new BlockchainComplianceService();
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, new PredictiveComplianceRiskService(auditService, regulationService), blockchainService);
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
        var blockchainService = new BlockchainComplianceService();
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, new PredictiveComplianceRiskService(auditService, regulationService), blockchainService);

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
