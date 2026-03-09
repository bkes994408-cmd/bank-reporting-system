using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IReportHistoryArchiveService
{
    Task<int> ArchiveAsync(ReportHistoriesRequest request, CancellationToken cancellationToken = default);
    ArchivedReportHistoriesPayload Query(ArchivedReportHistoriesQueryRequest request);
}

public class ReportHistoryArchiveService : IReportHistoryArchiveService
{
    private readonly IAgentService _agentService;
    private readonly object _lock = new();
    private readonly List<ArchivedReportHistoryRecord> _records = new();

    public ReportHistoryArchiveService(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public async Task<int> ArchiveAsync(ReportHistoriesRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _agentService.GetReportHistoriesAsync(request);
        if (response.Code != "0000")
        {
            throw new InvalidOperationException($"archive failed: {response.Code} {response.Msg}");
        }

        var archivedAtUtc = DateTime.UtcNow;
        var incoming = (response.Payload?.Reports ?? new List<ReportHistory>())
            .Select(r => new ArchivedReportHistoryRecord
            {
                BankCode = request.BankCode,
                ReportId = request.ReportId,
                Year = request.Year,
                Type = request.Type,
                Report = r,
                ArchivedAtUtc = archivedAtUtc
            })
            .ToList();

        if (incoming.Count == 0)
        {
            return 0;
        }

        lock (_lock)
        {
            var key = BuildKey(request.BankCode, request.ReportId, request.Year, request.Type);
            _records.RemoveAll(x => BuildKey(x.BankCode, x.ReportId, x.Year, x.Type) == key);
            _records.AddRange(incoming);
        }

        return incoming.Count;
    }

    public ArchivedReportHistoriesPayload Query(ArchivedReportHistoriesQueryRequest request)
    {
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 200);

        List<ArchivedReportHistoryRecord> snapshot;
        lock (_lock)
        {
            snapshot = _records.ToList();
        }

        var query = snapshot.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.BankCode))
        {
            query = query.Where(x => x.BankCode == request.BankCode);
        }
        if (!string.IsNullOrWhiteSpace(request.ReportId))
        {
            query = query.Where(x => x.ReportId == request.ReportId);
        }
        if (!string.IsNullOrWhiteSpace(request.Year))
        {
            query = query.Where(x => x.Year == request.Year);
        }
        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            query = query.Where(x => string.Equals(x.Type, request.Type, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(x => string.Equals(x.Report.Status, request.Status, StringComparison.OrdinalIgnoreCase));
        }

        query = query.OrderByDescending(x => x.ArchivedAtUtc).ThenByDescending(x => x.Report.QueryTime);

        var total = query.Count();
        var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new ArchivedReportHistoriesPayload
        {
            Total = total,
            Page = page,
            PageSize = pageSize,
            Reports = items
        };
    }

    private static string BuildKey(string bankCode, string reportId, string year, string? type)
        => $"{bankCode}::{reportId}::{year}::{type ?? string.Empty}";
}
