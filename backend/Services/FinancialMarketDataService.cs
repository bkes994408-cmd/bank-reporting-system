using System.Collections.Concurrent;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IFinancialMarketDataService
{
    FinancialMarketSnapshot Upsert(FinancialMarketSnapshotUpsertRequest request);
    FinancialMarketSnapshotQueryPayload Query(FinancialMarketSnapshotQueryRequest request);
    FinancialMarketSnapshot? GetLatest(TimeSpan maxAge);
}

public class FinancialMarketDataService : IFinancialMarketDataService
{
    private readonly ConcurrentQueue<FinancialMarketSnapshot> _snapshots = new();

    public FinancialMarketSnapshot Upsert(FinancialMarketSnapshotUpsertRequest request)
    {
        var snapshot = new FinancialMarketSnapshot
        {
            SnapshotId = $"mkt-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32],
            SourceName = request.SourceName,
            CapturedAtUtc = (request.CapturedAtUtc ?? DateTime.UtcNow).ToUniversalTime(),
            VolatilityIndex = Math.Max(0, request.VolatilityIndex),
            CreditSpreadBps = Math.Max(0, request.CreditSpreadBps),
            FxVolatilityPercent = Math.Max(0, request.FxVolatilityPercent),
            LiquidityStressLevel = NormalizeLiquidityStress(request.LiquidityStressLevel),
            Metadata = request.Metadata ?? new Dictionary<string, string>()
        };

        _snapshots.Enqueue(snapshot);
        while (_snapshots.Count > 5000 && _snapshots.TryDequeue(out _))
        {
        }

        return snapshot;
    }

    public FinancialMarketSnapshotQueryPayload Query(FinancialMarketSnapshotQueryRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = _snapshots.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(request.SourceName))
        {
            var sourceName = request.SourceName.Trim();
            query = query.Where(x => string.Equals(x.SourceName, sourceName, StringComparison.OrdinalIgnoreCase));
        }

        if (request.FromCapturedAtUtc.HasValue)
        {
            var fromUtc = request.FromCapturedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.CapturedAtUtc >= fromUtc);
        }

        if (request.ToCapturedAtUtc.HasValue)
        {
            var toUtc = request.ToCapturedAtUtc.Value.ToUniversalTime();
            query = query.Where(x => x.CapturedAtUtc <= toUtc);
        }

        var ordered = query.OrderByDescending(x => x.CapturedAtUtc).ToList();
        return new FinancialMarketSnapshotQueryPayload
        {
            Total = ordered.Count,
            Page = page,
            PageSize = pageSize,
            Records = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList()
        };
    }

    public FinancialMarketSnapshot? GetLatest(TimeSpan maxAge)
    {
        var now = DateTime.UtcNow;
        return _snapshots
            .OrderByDescending(x => x.CapturedAtUtc)
            .FirstOrDefault(x => now - x.CapturedAtUtc <= maxAge);
    }

    private static string NormalizeLiquidityStress(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is "low" or "medium" or "high" ? normalized : "medium";
    }
}
