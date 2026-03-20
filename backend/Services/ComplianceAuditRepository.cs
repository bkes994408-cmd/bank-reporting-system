using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

internal interface IComplianceAuditRepository
{
    void AddTrail(AuditTrailRecord record);
    List<AuditTrailRecord> SnapshotTrails();
    List<AuditTrailRecord> SnapshotTraceSource(string? traceId);
    List<AuditTrailRecord> SnapshotTrailSourceByUser(string? user);
    void AddReport(ComplianceAuditReportRecord report);
    List<ComplianceAuditReportRecord> SnapshotReports();
}

internal sealed class InMemoryComplianceAuditRepository : IComplianceAuditRepository
{
    private const int MaxTrailRecords = 10000;
    private const int MaxReportRecords = 2000;

    private readonly object _trailLock = new();
    private readonly object _reportLock = new();
    private readonly List<AuditTrailRecord> _trailRecords = [];
    private readonly Dictionary<string, Queue<AuditTrailRecord>> _traceIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Queue<AuditTrailRecord>> _userIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ComplianceAuditReportRecord> _reports = [];

    public void AddTrail(AuditTrailRecord record)
    {
        lock (_trailLock)
        {
            _trailRecords.Add(record);
            AddTraceIndex(record);
            AddUserIndex(record);

            if (_trailRecords.Count <= MaxTrailRecords)
            {
                return;
            }

            var removeCount = _trailRecords.Count - MaxTrailRecords;
            for (var i = 0; i < removeCount; i++)
            {
                RemoveTraceIndex(_trailRecords[i]);
                RemoveUserIndex(_trailRecords[i]);
            }

            _trailRecords.RemoveRange(0, removeCount);
        }
    }

    public List<AuditTrailRecord> SnapshotTrails()
    {
        lock (_trailLock)
        {
            return [.. _trailRecords];
        }
    }

    public List<AuditTrailRecord> SnapshotTraceSource(string? traceId)
    {
        lock (_trailLock)
        {
            if (!string.IsNullOrWhiteSpace(traceId) && _traceIndex.TryGetValue(traceId.Trim(), out var queue))
            {
                return [.. queue];
            }

            return [.. _trailRecords];
        }
    }

    public List<AuditTrailRecord> SnapshotTrailSourceByUser(string? user)
    {
        lock (_trailLock)
        {
            if (!string.IsNullOrWhiteSpace(user) && _userIndex.TryGetValue(user.Trim(), out var queue))
            {
                return [.. queue];
            }

            return [.. _trailRecords];
        }
    }

    public void AddReport(ComplianceAuditReportRecord report)
    {
        lock (_reportLock)
        {
            _reports.Add(report);
            if (_reports.Count > MaxReportRecords)
            {
                _reports.RemoveRange(0, _reports.Count - MaxReportRecords);
            }
        }
    }

    public List<ComplianceAuditReportRecord> SnapshotReports()
    {
        lock (_reportLock)
        {
            return [.. _reports];
        }
    }

    private void AddTraceIndex(AuditTrailRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TraceId))
        {
            return;
        }

        var key = record.TraceId.Trim();
        if (!_traceIndex.TryGetValue(key, out var queue))
        {
            queue = new Queue<AuditTrailRecord>();
            _traceIndex[key] = queue;
        }

        queue.Enqueue(record);
    }

    private void RemoveTraceIndex(AuditTrailRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TraceId))
        {
            return;
        }

        var key = record.TraceId.Trim();
        RemoveQueueRecord(_traceIndex, key, record);
    }

    private void AddUserIndex(AuditTrailRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.User))
        {
            return;
        }

        var key = record.User.Trim();
        if (!_userIndex.TryGetValue(key, out var queue))
        {
            queue = new Queue<AuditTrailRecord>();
            _userIndex[key] = queue;
        }

        queue.Enqueue(record);
    }

    private void RemoveUserIndex(AuditTrailRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.User))
        {
            return;
        }

        var key = record.User.Trim();
        RemoveQueueRecord(_userIndex, key, record);
    }

    private static void RemoveQueueRecord(Dictionary<string, Queue<AuditTrailRecord>> index, string key, AuditTrailRecord record)
    {
        if (!index.TryGetValue(key, out var queue) || queue.Count == 0)
        {
            return;
        }

        if (ReferenceEquals(queue.Peek(), record))
        {
            queue.Dequeue();
        }
        else
        {
            var rebuilt = new Queue<AuditTrailRecord>(queue.Count);
            while (queue.TryDequeue(out var entry))
            {
                if (!ReferenceEquals(entry, record))
                {
                    rebuilt.Enqueue(entry);
                }
            }

            index[key] = rebuilt;
            queue = rebuilt;
        }

        if (queue.Count == 0)
        {
            index.Remove(key);
        }
    }
}
