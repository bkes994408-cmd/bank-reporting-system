using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

internal interface IComplianceAuditRepository
{
    void AddTrail(AuditTrailRecord record);
    List<AuditTrailRecord> SnapshotTrails();
    List<AuditTrailRecord> SnapshotTraceSource(string? traceId);
    List<AuditTrailRecord> SnapshotTrailSourceByUser(string? user);
    List<AuditTrailRecord> SnapshotTrailSourceByPath(string? path);
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
    private readonly Dictionary<string, Queue<AuditTrailRecord>> _pathIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ComplianceAuditReportRecord> _reports = [];

    public void AddTrail(AuditTrailRecord record)
    {
        lock (_trailLock)
        {
            _trailRecords.Add(record);
            AddIndexRecord(_traceIndex, record.TraceId, record);
            AddIndexRecord(_userIndex, record.User, record);
            AddIndexRecord(_pathIndex, record.Path, record, NormalizePathKey);

            if (_trailRecords.Count <= MaxTrailRecords)
            {
                return;
            }

            var removeCount = _trailRecords.Count - MaxTrailRecords;
            for (var i = 0; i < removeCount; i++)
            {
                var target = _trailRecords[i];
                RemoveIndexRecord(_traceIndex, target.TraceId, target);
                RemoveIndexRecord(_userIndex, target.User, target);
                RemoveIndexRecord(_pathIndex, target.Path, target, NormalizePathKey);
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

    public List<AuditTrailRecord> SnapshotTrailSourceByPath(string? path)
    {
        lock (_trailLock)
        {
            var key = NormalizePathKey(path);
            if (key is not null && _pathIndex.TryGetValue(key, out var queue))
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

    private static void AddIndexRecord(
        Dictionary<string, Queue<AuditTrailRecord>> index,
        string? rawKey,
        AuditTrailRecord record,
        Func<string?, string?>? keyNormalizer = null)
    {
        var key = keyNormalizer is null
            ? NormalizeGenericKey(rawKey)
            : keyNormalizer(rawKey);

        if (key is null)
        {
            return;
        }

        if (!index.TryGetValue(key, out var queue))
        {
            queue = new Queue<AuditTrailRecord>();
            index[key] = queue;
        }

        queue.Enqueue(record);
    }

    private static void RemoveIndexRecord(
        Dictionary<string, Queue<AuditTrailRecord>> index,
        string? rawKey,
        AuditTrailRecord record,
        Func<string?, string?>? keyNormalizer = null)
    {
        var key = keyNormalizer is null
            ? NormalizeGenericKey(rawKey)
            : keyNormalizer(rawKey);

        if (key is null)
        {
            return;
        }

        RemoveQueueRecord(index, key, record);
    }

    private static string? NormalizeGenericKey(string? rawKey)
        => string.IsNullOrWhiteSpace(rawKey) ? null : rawKey.Trim();

    private static string? NormalizePathKey(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        var normalized = rawPath.Trim();
        if (normalized.Length > 1)
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static void RemoveQueueRecord(Dictionary<string, Queue<AuditTrailRecord>> index, string key, AuditTrailRecord record)
    {
        if (!index.TryGetValue(key, out var queue) || queue.Count == 0)
        {
            return;
        }

        // 依照 AddTrail/compaction 移除順序，同 key 的 queue 一定維持 FIFO 對齊
        if (!ReferenceEquals(queue.Peek(), record))
        {
            return;
        }

        queue.Dequeue();

        if (queue.Count == 0)
        {
            index.Remove(key);
        }
    }
}
