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
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, blockchainService);
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
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, blockchainService);

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
