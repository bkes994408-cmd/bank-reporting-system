using System.Collections.Concurrent;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IPredictiveComplianceRiskService
{
    PredictiveComplianceRiskReport Assess(PredictiveComplianceRiskAssessRequest request);
    PredictiveComplianceRiskQueryPayload Query(PredictiveComplianceRiskQueryRequest request);
}

public class PredictiveComplianceRiskService : IPredictiveComplianceRiskService
{
    private readonly IComplianceAuditService _complianceAuditService;
    private readonly IRegulationMonitoringService _regulationMonitoringService;
    private readonly IFinancialMarketDataService _financialMarketDataService;
    private readonly ConcurrentQueue<PredictiveComplianceRiskReport> _reports = new();

    public PredictiveComplianceRiskService(
        IComplianceAuditService complianceAuditService,
        IRegulationMonitoringService regulationMonitoringService,
        IFinancialMarketDataService? financialMarketDataService = null)
    {
        _complianceAuditService = complianceAuditService;
        _regulationMonitoringService = regulationMonitoringService;
        _financialMarketDataService = financialMarketDataService ?? new FinancialMarketDataService();
    }

    public PredictiveComplianceRiskReport Assess(PredictiveComplianceRiskAssessRequest request)
    {
        var lookbackDays = Math.Clamp(request.LookbackDays, 7, 180);
        var forecastDays = Math.Clamp(request.ForecastDays, 1, 90);
        var now = DateTime.UtcNow;
        var start = now.AddDays(-lookbackDays);

        var trails = _complianceAuditService.QueryTrails(new AuditTrailQueryRequest
        {
            StartDateUtc = start,
            EndDateUtc = now,
            Page = 1,
            PageSize = 500
        }).Records;

        var impactReports = _regulationMonitoringService.QueryImpactReports(new RegulationImpactQueryRequest
        {
            Source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim(),
            DocumentCode = string.IsNullOrWhiteSpace(request.DocumentCode) ? null : request.DocumentCode.Trim(),
            FromGeneratedAtUtc = now.AddDays(-Math.Max(lookbackDays, 30)),
            ToGeneratedAtUtc = now,
            Page = 1,
            PageSize = 50
        }).Records;

        var focusAreas = (request.FocusAreas ?? new List<string>())
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var factors = new List<PredictiveComplianceRiskFactor>();

        var totalRequests = trails.Count;
        var failedRequests = trails.Count(x => x.StatusCode >= 400);
        var failureRate = totalRequests == 0 ? 0d : failedRequests / (double)totalRequests;
        factors.Add(new PredictiveComplianceRiskFactor
        {
            FactorKey = "failure_rate",
            FactorName = "歷史失敗率",
            Score = Scale(failureRate, 0.03, 0.25),
            Evidence = $"{failedRequests}/{totalRequests} requests failed ({failureRate:P1})"
        });

        var sensitiveRate = totalRequests == 0 ? 0d : trails.Count(x => x.IsSensitiveOperation) / (double)totalRequests;
        factors.Add(new PredictiveComplianceRiskFactor
        {
            FactorKey = "sensitive_operation_ratio",
            FactorName = "敏感操作占比",
            Score = Scale(sensitiveRate, 0.15, 0.55),
            Evidence = $"sensitive ratio {sensitiveRate:P1}"
        });

        var highRiskRate = totalRequests == 0 ? 0d : trails.Count(x => string.Equals(x.RiskLevel, "high", StringComparison.OrdinalIgnoreCase)) / (double)totalRequests;
        factors.Add(new PredictiveComplianceRiskFactor
        {
            FactorKey = "high_risk_operation_ratio",
            FactorName = "高風險操作占比",
            Score = Scale(highRiskRate, 0.08, 0.35),
            Evidence = $"high-risk ratio {highRiskRate:P1}"
        });

        var regulationChanges = impactReports.Sum(x => x.Changes.Count);
        var highRegulationImpacts = impactReports.SelectMany(x => x.ImpactAreas).Count(x => string.Equals(x.Severity, "high", StringComparison.OrdinalIgnoreCase));
        factors.Add(new PredictiveComplianceRiskFactor
        {
            FactorKey = "regulation_change_pressure",
            FactorName = "外部法規變動壓力",
            Score = Scale(regulationChanges + highRegulationImpacts * 2, 2, 20),
            Evidence = $"changes={regulationChanges}, highImpactAreas={highRegulationImpacts}"
        });

        if (focusAreas.Count > 0)
        {
            var focusHits = impactReports
                .SelectMany(x => x.ImpactAreas)
                .Count(x => focusAreas.Contains(x.Domain));
            factors.Add(new PredictiveComplianceRiskFactor
            {
                FactorKey = "focus_domain_exposure",
                FactorName = "重點領域暴露度",
                Score = Scale(focusHits, 1, 8),
                Evidence = $"focus areas hit {focusHits} times"
            });
        }

        var latestMarketSnapshot = _financialMarketDataService.GetLatest(TimeSpan.FromHours(24));
        if (latestMarketSnapshot is not null)
        {
            var liquidityStressScore = latestMarketSnapshot.LiquidityStressLevel switch
            {
                "high" => 100,
                "medium" => 60,
                _ => 20
            };

            var marketScore = new[]
            {
                Scale(latestMarketSnapshot.VolatilityIndex, 18, 45),
                Scale(latestMarketSnapshot.CreditSpreadBps, 80, 260),
                Scale(latestMarketSnapshot.FxVolatilityPercent, 4, 16),
                liquidityStressScore
            }.Average();

            factors.Add(new PredictiveComplianceRiskFactor
            {
                FactorKey = "real_time_market_stress",
                FactorName = "即時市場壓力",
                Score = marketScore,
                Evidence = $"source={latestMarketSnapshot.SourceName}, vix={latestMarketSnapshot.VolatilityIndex:F1}, creditSpread={latestMarketSnapshot.CreditSpreadBps:F1}bps, fxVol={latestMarketSnapshot.FxVolatilityPercent:F1}%, liquidity={latestMarketSnapshot.LiquidityStressLevel}"
            });
        }

        var weightedScore = (int)Math.Round(factors.Average(x => x.Score));
        var trend = BuildTrendForecast(trails, now, forecastDays, weightedScore);
        factors.Add(new PredictiveComplianceRiskFactor
        {
            FactorKey = "risk_trend_acceleration",
            FactorName = "風險走勢加速度",
            Score = Scale(Math.Abs(trend.SlopePerDay), 2, 18),
            Evidence = $"direction={trend.Direction}, slope/day={trend.SlopePerDay:F2}"
        });

        weightedScore = (int)Math.Round(factors.Average(x => x.Score));
        var confidence = BuildConfidence(totalRequests, impactReports.Count);
        var level = ToRiskLevel(weightedScore);

        var report = new PredictiveComplianceRiskReport
        {
            AssessmentId = $"pred-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            GeneratedAtUtc = now,
            LookbackDays = lookbackDays,
            ForecastDays = forecastDays,
            PredictedRiskLevel = level,
            RiskScore = weightedScore,
            ConfidenceScore = confidence,
            TrendForecast = trend,
            Factors = factors.OrderByDescending(x => x.Score).ToList(),
            EarlyWarnings = BuildWarnings(level, failureRate, highRegulationImpacts, trend),
            RecommendedActions = BuildActions(level, trend)
        };

        _reports.Enqueue(report);
        while (_reports.Count > 2000 && _reports.TryDequeue(out _))
        {
        }

        return report;
    }

    public PredictiveComplianceRiskQueryPayload Query(PredictiveComplianceRiskQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _reports.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.RiskLevel))
        {
            var riskLevel = request.RiskLevel.Trim();
            query = query.Where(x => string.Equals(x.PredictedRiskLevel, riskLevel, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FromGeneratedAtUtc.HasValue)
        {
            var fromUtc = request.FromGeneratedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.GeneratedAtUtc >= fromUtc);
        }

        if (request.ToGeneratedAtUtc.HasValue)
        {
            var toUtc = request.ToGeneratedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.GeneratedAtUtc <= toUtc);
        }

        var ordered = query.OrderByDescending(x => x.GeneratedAtUtc).ToList();
        return new PredictiveComplianceRiskQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Reports = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    private static PredictiveComplianceRiskTrendForecast BuildTrendForecast(
        IReadOnlyCollection<AuditTrailRecord> trails,
        DateTime nowUtc,
        int forecastDays,
        int fallbackScore)
    {
        var grouped = trails
            .GroupBy(x => x.TimestampUtc.Date)
            .OrderBy(x => x.Key)
            .ToList();

        if (grouped.Count == 0)
        {
            return BuildFlatForecast(nowUtc, forecastDays, fallbackScore);
        }

        var points = grouped.Select((g, i) =>
        {
            var total = g.Count();
            var failed = g.Count(x => x.StatusCode >= 400);
            var highRisk = g.Count(x => string.Equals(x.RiskLevel, "high", StringComparison.OrdinalIgnoreCase));
            var sensitive = g.Count(x => x.IsSensitiveOperation);

            var score = (int)Math.Round(new[]
            {
                Scale(total == 0 ? 0 : failed / (double)total, 0.02, 0.25),
                Scale(total == 0 ? 0 : highRisk / (double)total, 0.08, 0.35),
                Scale(total == 0 ? 0 : sensitive / (double)total, 0.15, 0.55)
            }.Average());

            return (Index: i, Score: score);
        }).ToList();

        var slope = LinearSlope(points.Select(x => (double)x.Index).ToList(), points.Select(x => (double)x.Score).ToList());
        var intercept = points.Average(x => x.Score) - slope * points.Average(x => x.Index);

        var forecast = new List<PredictiveComplianceRiskTrendPoint>();
        for (var day = 1; day <= forecastDays; day++)
        {
            var x = points.Count - 1 + day;
            var predicted = Math.Clamp((int)Math.Round(intercept + slope * x), 0, 100);
            forecast.Add(new PredictiveComplianceRiskTrendPoint
            {
                DateUtc = nowUtc.Date.AddDays(day),
                PredictedRiskScore = predicted,
                PredictedRiskLevel = ToRiskLevel(predicted)
            });
        }

        var avg = forecast.Count == 0 ? fallbackScore : (int)Math.Round(forecast.Average(x => x.PredictedRiskScore));
        var peak = forecast.Count == 0 ? fallbackScore : forecast.Max(x => x.PredictedRiskScore);

        return new PredictiveComplianceRiskTrendForecast
        {
            Direction = ToDirection(slope),
            SlopePerDay = Math.Round(slope, 2),
            ProjectedAverageRiskScore = avg,
            ProjectedPeakRiskScore = peak,
            Points = forecast
        };
    }

    private static PredictiveComplianceRiskTrendForecast BuildFlatForecast(DateTime nowUtc, int forecastDays, int score)
    {
        var points = Enumerable.Range(1, forecastDays)
            .Select(day => new PredictiveComplianceRiskTrendPoint
            {
                DateUtc = nowUtc.Date.AddDays(day),
                PredictedRiskScore = score,
                PredictedRiskLevel = ToRiskLevel(score)
            })
            .ToList();

        return new PredictiveComplianceRiskTrendForecast
        {
            Direction = "stable",
            SlopePerDay = 0,
            ProjectedAverageRiskScore = score,
            ProjectedPeakRiskScore = score,
            Points = points
        };
    }

    private static double LinearSlope(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        if (x.Count != y.Count || x.Count < 2)
        {
            return 0;
        }

        var xAvg = x.Average();
        var yAvg = y.Average();
        var numerator = 0d;
        var denominator = 0d;

        for (var i = 0; i < x.Count; i++)
        {
            var xDiff = x[i] - xAvg;
            numerator += xDiff * (y[i] - yAvg);
            denominator += xDiff * xDiff;
        }

        if (denominator <= 0)
        {
            return 0;
        }

        return numerator / denominator;
    }

    private static string ToDirection(double slope)
    {
        if (slope >= 3.5) return "rising_fast";
        if (slope >= 1.2) return "rising";
        if (slope <= -3.5) return "falling_fast";
        if (slope <= -1.2) return "falling";
        return "stable";
    }

    private static int BuildConfidence(int totalRequests, int impactReportCount)
    {
        var requestConfidence = totalRequests switch
        {
            >= 300 => 45,
            >= 120 => 35,
            >= 40 => 25,
            > 0 => 15,
            _ => 5
        };

        var regulationConfidence = impactReportCount switch
        {
            >= 10 => 40,
            >= 5 => 30,
            >= 2 => 20,
            1 => 10,
            _ => 0
        };

        return Math.Clamp(requestConfidence + regulationConfidence + 10, 10, 95);
    }

    private static string ToRiskLevel(int score)
    {
        if (score >= 80) return "critical";
        if (score >= 65) return "high";
        if (score >= 45) return "medium";
        return "low";
    }

    private static List<string> BuildWarnings(string riskLevel, double failureRate, int highRegulationImpacts, PredictiveComplianceRiskTrendForecast trend)
    {
        var warnings = new List<string>();
        if (failureRate >= 0.2)
        {
            warnings.Add("近期失敗率偏高，未來 1-2 週內觸發合規異常告警機率上升。");
        }

        if (highRegulationImpacts >= 2)
        {
            warnings.Add("外部法規高衝擊變動密集，現行流程可能在下個申報週期出現不符合項。");
        }

        if (trend.Direction is "rising" or "rising_fast")
        {
            warnings.Add($"風險走勢呈{(trend.Direction == "rising_fast" ? "快速上升" : "上升")}，預測峰值分數 {trend.ProjectedPeakRiskScore}。");
        }

        if (riskLevel is "high" or "critical")
        {
            warnings.Add("預測風險等級偏高，建議啟動強化監控與二次覆核。");
        }

        if (warnings.Count == 0)
        {
            warnings.Add("目前風險訊號平穩，建議維持每週監控與月度回顧。");
        }

        return warnings;
    }

    private static List<string> BuildActions(string riskLevel, PredictiveComplianceRiskTrendForecast trend)
    {
        var actions = riskLevel switch
        {
            "critical" => new List<string>
            {
                "立即召開合規應變會議，凍結高風險流程變更。",
                "對高風險路徑啟用逐筆審核與雙人覆核。",
                "在 24 小時內完成法規差異修補清單。"
            },
            "high" => new List<string>
            {
                "提升告警閾值敏感度並每日追蹤風險趨勢。",
                "針對高風險操作加上額外驗證與抽查。"
            },
            "medium" => new List<string>
            {
                "安排每週合規風險檢視，補強易錯端點的測試覆蓋。"
            },
            _ => new List<string>
            {
                "維持現行控管，持續監測外部法規更新。"
            }
        };

        if (trend.Direction is "rising" or "rising_fast")
        {
            actions.Add("將預測風險走勢納入下次合規報告審查附錄，並追蹤上升斜率是否收斂。");
        }

        return actions;
    }

    private static double Scale(double value, double low, double high)
    {
        if (high <= low)
        {
            return 0;
        }

        var normalized = (value - low) / (high - low);
        return Math.Clamp(normalized * 100, 0, 100);
    }
}
