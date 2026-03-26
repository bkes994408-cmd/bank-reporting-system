using BankReporting.Api;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<AppState>();
builder.Services.AddSingleton(new SessionStore(TimeSpan.FromHours(8)));
builder.Services.AddSingleton<JsonStateRepository>();
builder.Services.AddSingleton<AdAuthenticator>();
builder.Services.AddSingleton<CompositeNotificationSink>();
builder.Services.AddHttpClient();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();

var db = app.Services.GetRequiredService<AppState>();
var sessions = app.Services.GetRequiredService<SessionStore>();
var repo = app.Services.GetRequiredService<JsonStateRepository>();
if (!repo.TryLoad(db, sessions)) Seed(db);

app.MapGet("/health", () => Results.Ok(new { status = "ok", ts = DateTimeOffset.UtcNow }));

app.MapPost("/auth/register", (AppState state, RegisterRequest req, HttpContext ctx) =>
{
    if (!SecurityHelpers.IsStrongPassword(req.Password)) return Results.BadRequest("Weak password");
    if (state.Users.Values.Any(u => u.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase))) return Results.Conflict("Email exists");

    var hash = SecurityHelpers.HashPassword(req.Password);
    var user = new User(Guid.NewGuid(), req.Name, req.Email, req.InstitutionCode, req.Department, req.Title, UserRole.Clerk, AccountStatus.PendingApproval,
        hash, new() { hash }, DateTimeOffset.UtcNow, 0, null, false, null, req.IsAdUser);
    state.Users[user.Id] = user;
    Audit(state, user.Id, user.Name, "REG-001", "User", user.Id.ToString(), "Account registration submitted", ctx);
    Persist(state, sessions, repo);
    return Results.Ok(new { userId = user.Id, status = user.Status.ToString() });
});

app.MapPost("/auth/login", (AppState state, SessionStore sessionStore, AdAuthenticator ad, LoginRequest req, HttpContext ctx) =>
{
    var user = state.Users.Values.FirstOrDefault(x => x.Email.Equals(req.Email, StringComparison.OrdinalIgnoreCase));
    if (user is null) return Results.Unauthorized();

    if (user.LockoutEnd is not null && user.LockoutEnd > DateTimeOffset.UtcNow) return Results.BadRequest("Locked");

    var passOk = user.IsAdUser ? ad.Validate(req.Email, req.Password) : SecurityHelpers.VerifyPassword(req.Password, user.PasswordHash);
    if (!passOk)
    {
        var failed = user.FailedLoginCount + 1;
        var updated = user with { FailedLoginCount = failed, LockoutEnd = failed >= 5 ? DateTimeOffset.UtcNow.AddMinutes(30) : null, Status = failed >= 5 ? AccountStatus.Locked : user.Status };
        state.Users[user.Id] = updated;
        Audit(state, user.Id, user.Name, "AUTH-002", "User", user.Id.ToString(), "Login failed", ctx);
        Persist(state, sessions, repo);
        return Results.Unauthorized();
    }

    if (user.Status is not AccountStatus.Active and not AccountStatus.PendingApproval) return Results.BadRequest($"Account status: {user.Status}");

    if (user.MfaEnabled)
    {
        if (string.IsNullOrWhiteSpace(req.MfaCode) || string.IsNullOrWhiteSpace(user.MfaSecret) || !SecurityHelpers.VerifyTotp(user.MfaSecret, req.MfaCode))
            return Results.BadRequest("MFA required or invalid");
    }

    var token = sessionStore.Create(user.Id);
    state.Users[user.Id] = user with { FailedLoginCount = 0, LockoutEnd = null };
    Audit(state, user.Id, user.Name, "AUTH-001", "Session", token, "Login success", ctx);
    Persist(state, sessions, repo);
    return Results.Ok(new
    {
        token,
        expiresInSeconds = 8 * 3600,
        user = new { user.Id, user.Name, user.Email, role = user.Role.ToString(), user.InstitutionCode, status = user.Status.ToString(), user.MfaEnabled, user.IsAdUser }
    });
});

app.MapPost("/auth/logout", (SessionStore sessionStore, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Auth-Token", out var h)) return Results.Ok();
    sessionStore.Remove(h.ToString());
    return Results.Ok();
});

app.MapPost("/auth/mfa/setup", (AppState state, SessionStore sessionStore, JsonStateRepository storage, HttpContext ctx) =>
{
    var user = CurrentUser(state, sessionStore, ctx);
    if (user is null) return Results.Unauthorized();
    var secret = SecurityHelpers.NewTotpSecret();
    state.Users[user.Id] = user with { MfaSecret = secret, MfaEnabled = false };
    Persist(state, sessions, storage);
    return Results.Ok(new { secret });
});

app.MapPost("/auth/mfa/enable", (AppState state, SessionStore sessionStore, JsonStateRepository storage, EnableMfaRequest req, HttpContext ctx) =>
{
    var user = CurrentUser(state, sessionStore, ctx);
    if (user is null) return Results.Unauthorized();
    if (string.IsNullOrWhiteSpace(user.MfaSecret) || !SecurityHelpers.VerifyTotp(user.MfaSecret, req.Code)) return Results.BadRequest("invalid code");
    state.Users[user.Id] = user with { MfaEnabled = true };
    Persist(state, sessions, storage);
    return Results.Ok(new { enabled = true });
});

app.MapPost("/admin/users/{userId:guid}/approve", (AppState state, SessionStore sessionStore, JsonStateRepository storage, CompositeNotificationSink sink, Guid userId, ApproveUserRequest req, HttpContext ctx) =>
{
    var admin = RequireRole(state, sessionStore, ctx, UserRole.Admin);
    if (admin is null) return Results.Unauthorized();
    if (!state.Users.TryGetValue(userId, out var user)) return Results.NotFound();
    var updated = user with { Role = req.Role, InstitutionCode = req.InstitutionCode, Status = req.Approve ? AccountStatus.Active : AccountStatus.Rejected };
    state.Users[userId] = updated;
    Notify(state, sink, userId, "Account", req.Approve ? "帳號申請已核准" : "帳號申請遭拒絕");
    Audit(state, admin.Id, admin.Name, "REG-004", "User", userId.ToString(), req.Approve ? "Approved" : "Rejected", ctx);
    Persist(state, sessions, storage);
    return Results.Ok(updated);
});

app.MapGet("/admin/users", (AppState state, SessionStore sessionStore, HttpContext ctx) =>
{
    var admin = RequireRole(state, sessionStore, ctx, UserRole.Admin);
    if (admin is null) return Results.Unauthorized();
    return Results.Ok(state.Users.Values.OrderBy(x => x.Email));
});

app.MapGet("/report-definitions", (AppState state) => Results.Ok(state.ReportDefinitions.Values.OrderBy(x => x.ReportCode)));

app.MapPost("/submissions", (AppState state, SessionStore sessionStore, JsonStateRepository storage, CreateSubmissionRequest req, HttpContext ctx) =>
{
    var user = RequireAny(state, sessionStore, ctx, UserRole.Clerk, UserRole.Supervisor, UserRole.Admin);
    if (user is null) return Results.Unauthorized();
    if (!state.ReportDefinitions.TryGetValue(req.ReportCode, out var def) || !def.IsEnabled) return Results.BadRequest("Invalid report code");

    if (state.Submissions.Values.Any(x => x.ReportCode == req.ReportCode && x.Period == req.Period && x.InstitutionCode == user.InstitutionCode && x.Status is SubmissionStatus.Pending or SubmissionStatus.Approved or SubmissionStatus.Submitted))
        return Results.BadRequest("Duplicate in same period");

    var version = def.Versions.Where(v => v.EffectiveDate <= DateOnly.Parse(req.Period + "-01")).OrderByDescending(v => v.EffectiveDate).FirstOrDefault() ?? def.Versions.OrderByDescending(v => v.EffectiveDate).First();

    using var payload = JsonDocument.Parse(req.Payload.GetRawText());
    foreach (var field in version.Fields.Where(f => f.Required))
        if (!payload.RootElement.TryGetProperty(field.FieldCode, out _))
            return Results.BadRequest($"Missing field {field.FieldCode}");

    var now = DateTimeOffset.UtcNow;
    var sub = new ReportSubmission(Guid.NewGuid(), req.ReportCode, req.Period, user.InstitutionCode, user.Id, null, version.Version, SubmissionStatus.Draft,
        JsonDocument.Parse(req.Payload.GetRawText()), 0, new(), now, now, null, null);
    state.Submissions[sub.Id] = sub;
    Audit(state, user.Id, user.Name, "WF-001", "Submission", sub.Id.ToString(), "Created draft", ctx);
    Persist(state, sessions, storage);
    return Results.Ok(sub);
});

app.MapPost("/submissions/{id:guid}/submit", (AppState state, SessionStore sessionStore, JsonStateRepository storage, CompositeNotificationSink sink, Guid id, HttpContext ctx) =>
{
    var user = RequireAny(state, sessionStore, ctx, UserRole.Clerk, UserRole.Supervisor, UserRole.Admin);
    if (user is null) return Results.Unauthorized();
    if (!state.Submissions.TryGetValue(id, out var sub)) return Results.NotFound();
    if (sub.SubmitterId != user.Id && user.Role != UserRole.Admin) return Results.Forbid();
    if (sub.Status is not SubmissionStatus.Draft and not SubmissionStatus.Rejected) return Results.BadRequest("Invalid state");

    var updated = sub with { Status = SubmissionStatus.Pending, UpdatedAt = DateTimeOffset.UtcNow };
    state.Submissions[id] = updated;
    NotifyRole(state, sink, user.InstitutionCode, UserRole.Supervisor, "Workflow", $"報表 {sub.ReportCode}/{sub.Period} 待審核");
    Audit(state, user.Id, user.Name, "WF-003", "Submission", sub.Id.ToString(), "Submitted for review", ctx);
    Persist(state, sessions, storage);
    return Results.Ok(updated);
});

app.MapPost("/submissions/{id:guid}/review", (AppState state, SessionStore sessionStore, JsonStateRepository storage, CompositeNotificationSink sink, Guid id, ReviewRequest req, HttpContext ctx) =>
{
    var reviewer = RequireAny(state, sessionStore, ctx, UserRole.Supervisor, UserRole.Admin);
    if (reviewer is null) return Results.Unauthorized();
    if (!state.Submissions.TryGetValue(id, out var sub)) return Results.NotFound();
    if (sub.Status != SubmissionStatus.Pending) return Results.BadRequest("Not pending");
    if (reviewer.Role != UserRole.Admin && reviewer.InstitutionCode != sub.InstitutionCode) return Results.Forbid();

    var updated = req.Approve
        ? sub with { Status = SubmissionStatus.Approved, ReviewerId = reviewer.Id, UpdatedAt = DateTimeOffset.UtcNow }
        : sub with { Status = SubmissionStatus.Rejected, ReviewerId = reviewer.Id, UpdatedAt = DateTimeOffset.UtcNow, RejectCount = sub.RejectCount + 1, RejectReasons = sub.RejectReasons.Append(req.Reason ?? "").ToList() };
    state.Submissions[id] = updated;
    Notify(state, sink, sub.SubmitterId, "Workflow", req.Approve ? "主管已放行" : $"主管退回：{req.Reason}");

    if (req.Approve)
    {
        var submitting = updated with { Status = SubmissionStatus.Submitting, UpdatedAt = DateTimeOffset.UtcNow };
        state.Submissions[id] = submitting;
        var success = Random.Shared.Next(0, 100) >= 10;
        state.Submissions[id] = submitting with
        {
            Status = success ? SubmissionStatus.Submitted : SubmissionStatus.Failed,
            UpdatedAt = DateTimeOffset.UtcNow,
            ApiResponseCode = success ? "200" : "500",
            ApiResponseMessage = success ? "Submitted to JCIC" : "Simulated timeout/retry exceeded"
        };
    }

    Audit(state, reviewer.Id, reviewer.Name, req.Approve ? "WF-009" : "WF-010", "Submission", id.ToString(), req.Approve ? "Approved" : "Rejected", ctx);
    Persist(state, sessions, storage);
    return Results.Ok(state.Submissions[id]);
});

app.MapGet("/history/{id:guid}/download", (AppState state, SessionStore sessionStore, Guid id, HttpContext ctx, string format = "json") =>
{
    var user = RequireAny(state, sessionStore, ctx, UserRole.Admin, UserRole.Supervisor, UserRole.Clerk, UserRole.ReadOnly);
    if (user is null) return Results.Unauthorized();
    if (!state.Submissions.TryGetValue(id, out var sub)) return Results.NotFound();
    if (!CanDownload(user, sub)) return Results.Forbid();

    Audit(state, user.Id, user.Name, "HIS-018", "Submission", id.ToString(), $"download {format}", ctx);
    return format.ToLowerInvariant() switch
    {
        "csv" => Results.File(System.Text.Encoding.UTF8.GetBytes(SecurityHelpers.BuildCsv(sub.Payload)), "text/csv", $"{sub.ReportCode}_{sub.Period}.csv"),
        "xlsx" => Results.File(SecurityHelpers.BuildXlsx(sub.Payload), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{sub.ReportCode}_{sub.Period}.xlsx"),
        _ => Results.Json(sub.Payload.RootElement)
    };
});

app.MapGet("/notifications", (AppState state, SessionStore sessionStore, HttpContext ctx) =>
{
    var user = RequireAny(state, sessionStore, ctx, UserRole.Admin, UserRole.Supervisor, UserRole.Clerk, UserRole.ReadOnly);
    if (user is null) return Results.Unauthorized();
    return Results.Ok(state.Notifications.Where(x => x.UserId == user.Id).OrderByDescending(x => x.CreatedAt).Take(200));
});

app.MapGet("/admin/audit-logs", (AppState state, SessionStore sessionStore, HttpContext ctx, int top = 200) =>
{
    var admin = RequireRole(state, sessionStore, ctx, UserRole.Admin);
    if (admin is null) return Results.Unauthorized();
    return Results.Ok(state.AuditLogs.OrderByDescending(x => x.At).Take(top));
});

app.Run();

static void Persist(AppState db, SessionStore sessions, JsonStateRepository repo) => repo.Save(db, sessions);

static User? RequireRole(AppState db, SessionStore sessions, HttpContext ctx, UserRole role)
{
    var u = CurrentUser(db, sessions, ctx);
    return u is not null && u.Role == role ? u : null;
}

static User? RequireAny(AppState db, SessionStore sessions, HttpContext ctx, params UserRole[] roles)
{
    var u = CurrentUser(db, sessions, ctx);
    return u is not null && roles.Contains(u.Role) ? u : null;
}

static User? CurrentUser(AppState db, SessionStore sessions, HttpContext ctx)
{
    if (!ctx.Request.Headers.TryGetValue("X-Auth-Token", out var h)) return null;
    if (!sessions.TryGetUserId(h.ToString(), out var userId)) return null;
    return db.Users.TryGetValue(userId, out var user) ? user : null;
}

static bool CanDownload(User user, ReportSubmission sub) => user.Role switch
{
    UserRole.Admin => true,
    UserRole.Supervisor => sub.InstitutionCode == user.InstitutionCode && sub.Status is SubmissionStatus.Submitted or SubmissionStatus.Rejected,
    UserRole.Clerk => sub.SubmitterId == user.Id && sub.Status == SubmissionStatus.Submitted,
    UserRole.ReadOnly => sub.Status == SubmissionStatus.Submitted,
    _ => false
};

static void Notify(AppState db, CompositeNotificationSink sink, Guid userId, string type, string message)
{
    var item = new Notification(Guid.NewGuid(), userId, type, message, false, DateTimeOffset.UtcNow);
    db.Notifications.Add(item);
    _ = sink.PublishAsync(item);
}

static void NotifyRole(AppState db, CompositeNotificationSink sink, string institutionCode, UserRole role, string type, string message)
{
    foreach (var user in db.Users.Values.Where(x => x.InstitutionCode == institutionCode && x.Role == role && x.Status == AccountStatus.Active))
        Notify(db, sink, user.Id, type, message);
}

static void Audit(AppState db, Guid? userId, string userName, string action, string entityType, string entityId, string summary, HttpContext ctx)
    => db.AuditLogs.Add(new AuditLog(Guid.NewGuid(), DateTimeOffset.UtcNow, userId, userName, action, entityType, entityId, summary, ctx.Connection.RemoteIpAddress?.ToString() ?? "local"));

static void Seed(AppState db)
{
    db.Institutions["B001"] = new Institution("B001", "第一銀行", true);
    db.Institutions["B002"] = new Institution("B002", "第二銀行", true);

    var adminHash = SecurityHelpers.HashPassword("Admin#12345678");
    var admin = new User(Guid.NewGuid(), "System Admin", "admin@bank.local", "B001", "IT", "Admin", UserRole.Admin, AccountStatus.Active, adminHash, new() { adminHash }, DateTimeOffset.UtcNow, 0, null, false, null);
    var supHash = SecurityHelpers.HashPassword("Supervisor#1234");
    var sup = new User(Guid.NewGuid(), "B001 Supervisor", "supervisor@bank.local", "B001", "Risk", "Supervisor", UserRole.Supervisor, AccountStatus.Active, supHash, new() { supHash }, DateTimeOffset.UtcNow, 0, null, false, null);
    var clerkHash = SecurityHelpers.HashPassword("Clerk#12345678");
    var clerk = new User(Guid.NewGuid(), "B001 Clerk", "clerk@bank.local", "B001", "Risk", "Clerk", UserRole.Clerk, AccountStatus.Active, clerkHash, new() { clerkHash }, DateTimeOffset.UtcNow, 0, null, false, null);

    db.Users[admin.Id] = admin;
    db.Users[sup.Id] = sup;
    db.Users[clerk.Id] = clerk;

    var reports = new[] { ("AI330", "授信擔保品別分析表"), ("AI863", "海外分行授信顆粒化資料") };

    foreach (var (code, name) in reports)
    {
        var fields = new List<ReportField>
        {
            new("reportMonth", "申報月份", "string", true, null, null, 7),
            new("amount", "金額", "number", true, 0),
            new("note", "備註", "string", false, null, null, 200)
        };

        var schema = JsonSerializer.Serialize(new
        {
            type = "object",
            properties = new { reportMonth = new { type = "string" }, amount = new { type = "number" }, note = new { type = "string" } },
            required = new[] { "reportMonth", "amount" }
        });

        var v = new ReportVersion(Guid.NewGuid(), code, "1.0.0", new DateOnly(2026, 1, 1), "seed", true, fields, schema, "{}");
        db.ReportDefinitions[code] = new ReportDefinition(code, name, code == "AI863" ? "Granular" : "Monthly", 15, $"/api/report/{code.ToLowerInvariant()}", true, new() { v });
    }
}

public record RegisterRequest(string Name, string Email, string Password, string InstitutionCode, string Department, string Title, bool IsAdUser = false);
public record LoginRequest(string Email, string Password, string? MfaCode = null);
public record EnableMfaRequest(string Code);
public record ApproveUserRequest(bool Approve, UserRole Role, string InstitutionCode);
public record CreateSubmissionRequest(string ReportCode, string Period, JsonElement Payload);
public record ReviewRequest(bool Approve, string? Reason);
