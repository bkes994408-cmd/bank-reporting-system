using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BankReporting.Api;

public sealed class SessionStore
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new(StringComparer.Ordinal);
    private readonly TimeSpan _ttl;

    public SessionStore(TimeSpan ttl) => _ttl = ttl;

    public string Create(Guid userId)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var now = DateTimeOffset.UtcNow;
        _sessions[token] = new SessionInfo(token, userId, now, now.Add(_ttl));
        return token;
    }

    public bool TryGetUserId(string token, out Guid userId)
    {
        userId = default;
        if (!_sessions.TryGetValue(token, out var session)) return false;
        if (session.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }

        _sessions[token] = session with { ExpiresAt = DateTimeOffset.UtcNow.Add(_ttl) };
        userId = session.UserId;
        return true;
    }

    public void Remove(string token) => _sessions.TryRemove(token, out _);
    public IReadOnlyCollection<SessionInfo> Snapshot() => _sessions.Values.ToArray();
    public void Restore(IEnumerable<SessionInfo> sessions)
    {
        _sessions.Clear();
        foreach (var session in sessions.Where(s => s.ExpiresAt > DateTimeOffset.UtcNow))
            _sessions[session.Token] = session;
    }
}

public sealed class JsonStateRepository
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public JsonStateRepository(IConfiguration cfg)
    {
        _path = cfg["PERSISTENCE_FILE"] ?? Path.Combine(AppContext.BaseDirectory, "data", "app-state.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
    }

    public bool TryLoad(AppState state, SessionStore sessions)
    {
        if (!File.Exists(_path)) return false;
        var text = File.ReadAllText(_path);
        var snapshot = JsonSerializer.Deserialize<AppStateSnapshot>(text, _options);
        if (snapshot is null) return false;

        state.Users.Clear();
        foreach (var i in snapshot.Users) state.Users[i.Id] = i;

        state.Institutions.Clear();
        foreach (var i in snapshot.Institutions) state.Institutions[i.Code] = i;

        state.ReportDefinitions.Clear();
        foreach (var i in snapshot.ReportDefinitions) state.ReportDefinitions[i.ReportCode] = i;

        state.Submissions.Clear();
        foreach (var i in snapshot.Submissions)
            state.Submissions[i.Id] = i.ToDomain();

        state.Keys.Clear();
        foreach (var i in snapshot.Keys) state.Keys[i.KeyId] = i;

        state.ResetBags();
        foreach (var i in snapshot.Notifications) state.Notifications.Add(i);
        foreach (var i in snapshot.AuditLogs) state.AuditLogs.Add(i);

        sessions.Restore(snapshot.Sessions);
        return true;
    }

    public void Save(AppState state, SessionStore sessions)
    {
        var snapshot = new AppStateSnapshot(
            state.Users.Values.ToArray(),
            state.Institutions.Values.ToArray(),
            state.ReportDefinitions.Values.ToArray(),
            state.Submissions.Values.Select(SubmissionSnapshot.FromDomain).ToArray(),
            state.Keys.Values.ToArray(),
            state.Notifications.ToArray(),
            state.AuditLogs.ToArray(),
            sessions.Snapshot().ToArray());

        var json = JsonSerializer.Serialize(snapshot, _options);
        File.WriteAllText(_path, json);
    }
}

public sealed class CompositeNotificationSink
{
    private readonly ILogger<CompositeNotificationSink> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _webhookUrl;

    public CompositeNotificationSink(ILogger<CompositeNotificationSink> logger, IHttpClientFactory factory, IConfiguration cfg)
    {
        _logger = logger;
        _httpClient = factory.CreateClient();
        _webhookUrl = cfg["NOTIFICATION_WEBHOOK_URL"];
    }

    public async Task PublishAsync(Notification notification, CancellationToken ct = default)
    {
        _logger.LogInformation("Notification {Type} => user {UserId}: {Message}", notification.Type, notification.UserId, notification.Message);
        if (string.IsNullOrWhiteSpace(_webhookUrl)) return;

        try
        {
            using var resp = await _httpClient.PostAsJsonAsync(_webhookUrl, notification, ct);
            if (!resp.IsSuccessStatusCode)
                _logger.LogWarning("Notification webhook failed with status {Status}", resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Notification webhook publish failed");
        }
    }
}

public sealed class AdAuthenticator
{
    private readonly Dictionary<string, string> _users;
    public bool Enabled { get; }

    public AdAuthenticator(IConfiguration cfg)
    {
        Enabled = bool.TryParse(cfg["AD_ENABLED"], out var enabled) && enabled;
        var raw = cfg["AD_MOCK_USERS_JSON"];
        _users = string.IsNullOrWhiteSpace(raw)
            ? new(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? new(StringComparer.OrdinalIgnoreCase);
    }

    public bool Validate(string email, string password)
    {
        if (!Enabled) return false;
        return _users.TryGetValue(email, out var pw) && pw == password;
    }
}

public record SessionInfo(string Token, Guid UserId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public record AppStateSnapshot(
    User[] Users,
    Institution[] Institutions,
    ReportDefinition[] ReportDefinitions,
    SubmissionSnapshot[] Submissions,
    CryptoKey[] Keys,
    Notification[] Notifications,
    AuditLog[] AuditLogs,
    SessionInfo[] Sessions);

public record SubmissionSnapshot(
    Guid Id,
    string ReportCode,
    string Period,
    string InstitutionCode,
    Guid SubmitterId,
    Guid? ReviewerId,
    string ReportVersion,
    SubmissionStatus Status,
    string PayloadJson,
    int RejectCount,
    List<string> RejectReasons,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? ApiResponseCode,
    string? ApiResponseMessage)
{
    public static SubmissionSnapshot FromDomain(ReportSubmission item) => new(
        item.Id, item.ReportCode, item.Period, item.InstitutionCode, item.SubmitterId, item.ReviewerId, item.ReportVersion,
        item.Status, item.Payload.RootElement.GetRawText(), item.RejectCount, item.RejectReasons, item.CreatedAt, item.UpdatedAt, item.ApiResponseCode, item.ApiResponseMessage);

    public ReportSubmission ToDomain() => new(
        Id, ReportCode, Period, InstitutionCode, SubmitterId, ReviewerId, ReportVersion,
        Status, JsonDocument.Parse(PayloadJson), RejectCount, RejectReasons, CreatedAt, UpdatedAt, ApiResponseCode, ApiResponseMessage);
}
