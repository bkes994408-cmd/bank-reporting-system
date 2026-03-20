using BankReporting.Api.Controllers;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankReporting.Tests;

public class PredictiveComplianceRiskServiceTests
{
    [Fact]
    public async Task Assess_UsesHistoricalAndRegulationSignals_ReturnsHighRisk()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var service = new PredictiveComplianceRiskService(auditService, regulationService, new FinancialMarketDataService());

        for (var i = 0; i < 160; i++)
        {
            auditService.RecordAuditTrail(new AuditTrailRecord
            {
                TimestampUtc = DateTime.UtcNow.AddDays(-3),
                User = i % 2 == 0 ? "alice" : "bob",
                Method = "POST",
                Path = "/api/declare",
                StatusCode = i < 60 ? 500 : 200,
                IsSensitiveOperation = i < 120,
                RiskLevel = i < 90 ? "high" : "medium"
            });
        }

        regulationService.UpsertSnapshot(new RegulationSnapshotUpsertRequest
        {
            Source = "fsc",
            DocumentCode = "FSC-AML-001",
            Title = "AML 規範",
            Content = "第一條 應申報時限為五日。第二條 需新增交易欄位與資料蒐集要求。",
            PublishedAtUtc = DateTime.UtcNow.AddDays(-4)
        });

        regulationService.UpsertSnapshot(new RegulationSnapshotUpsertRequest
        {
            Source = "fsc",
            DocumentCode = "FSC-AML-001",
            Title = "AML 規範",
            Content = "第一條 應申報時限縮短為三日。第二條 新增高風險客戶KYC欄位與留存規範。",
            PublishedAtUtc = DateTime.UtcNow.AddDays(-2)
        });

        _ = await regulationService.AnalyzeLatestAsync(new RegulationImpactAnalysisRequest
        {
            Source = "fsc",
            DocumentCode = "FSC-AML-001"
        }, CancellationToken.None);

        var report = service.Assess(new PredictiveComplianceRiskAssessRequest
        {
            LookbackDays = 30,
            ForecastDays = 14,
            Source = "fsc",
            DocumentCode = "FSC-AML-001",
            FocusAreas = new List<string> { "申報流程", "數據採集" }
        });

        Assert.True(report.RiskScore >= 45);
        Assert.True(report.ConfidenceScore >= 40);
        Assert.Contains(report.Factors, x => x.FactorKey == "regulation_change_pressure");
        Assert.Contains(report.Factors, x => x.FactorKey == "regulation_change_pressure" && x.Score > 0);
        Assert.NotNull(report.TrendForecast);
        Assert.Equal(14, report.TrendForecast.Points.Count);
    }

    [Fact]
    public void Query_FiltersByRiskLevel()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var service = new PredictiveComplianceRiskService(auditService, regulationService, new FinancialMarketDataService());

        _ = service.Assess(new PredictiveComplianceRiskAssessRequest { LookbackDays = 30, ForecastDays = 7 });

        var result = service.Query(new PredictiveComplianceRiskQueryRequest
        {
            RiskLevel = "low",
            Page = 1,
            PageSize = 10
        });

        Assert.True(result.Total >= 1);
        Assert.All(result.Reports, x => Assert.Equal("low", x.PredictedRiskLevel));
    }

    [Fact]
    public void Assess_WhenRealtimeMarketStressPresent_AddsMarketStressFactor()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var marketDataService = new FinancialMarketDataService();
        marketDataService.Upsert(new FinancialMarketSnapshotUpsertRequest
        {
            SourceName = "twse-realtime-feed",
            CapturedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            VolatilityIndex = 39,
            CreditSpreadBps = 220,
            FxVolatilityPercent = 13,
            LiquidityStressLevel = "high"
        });

        var service = new PredictiveComplianceRiskService(auditService, regulationService, marketDataService);
        var report = service.Assess(new PredictiveComplianceRiskAssessRequest());

        Assert.Contains(report.Factors, x => x.FactorKey == "real_time_market_stress");
        Assert.Contains(report.Factors, x => x.FactorKey == "real_time_market_stress" && x.Score >= 70);
        Assert.Contains(report.Factors, x => x.FactorKey == "risk_trend_acceleration");
    }
}

public class PredictiveComplianceRiskControllerTests
{
    [Fact]
    public void UpsertFinancialMarketSnapshot_ReturnsBadRequest_WhenSourceNameMissing()
    {
        var auditService = new ComplianceAuditService();
        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);
        var marketDataService = new FinancialMarketDataService();
        var predictiveService = new PredictiveComplianceRiskService(auditService, regulationService, marketDataService);
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, predictiveService, new BlockchainComplianceService(), marketDataService);

        var result = controller.UpsertFinancialMarketSnapshot(new FinancialMarketSnapshotUpsertRequest());

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void AssessPredictiveRisk_ReturnsOkPayload()
    {
        var auditService = new ComplianceAuditService();
        auditService.RecordAuditTrail(new AuditTrailRecord
        {
            TimestampUtc = DateTime.UtcNow.AddMinutes(-10),
            User = "ops",
            Method = "POST",
            Path = "/api/keys/import",
            StatusCode = 500,
            IsSensitiveOperation = true,
            RiskLevel = "high"
        });

        var regulationService = new RegulationMonitoringService();
        var externalService = new StubExternalComplianceDataService();
        var alertService = new ComplianceAlertService(auditService);
        var marketDataService = new FinancialMarketDataService();
        var predictiveService = new PredictiveComplianceRiskService(auditService, regulationService, marketDataService);
        var controller = new ComplianceController(auditService, regulationService, externalService, alertService, predictiveService, new BlockchainComplianceService(), marketDataService);

        var result = controller.AssessPredictiveRisk(new PredictiveComplianceRiskAssessRequest
        {
            LookbackDays = 14,
            ForecastDays = 7,
            Source = " fsc "
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ApiResponse<PredictiveComplianceRiskReport>>(ok.Value);
        Assert.Equal("0000", payload.Code);
        Assert.NotNull(payload.Payload);
    }
}
