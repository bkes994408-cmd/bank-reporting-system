namespace BankReporting.Api.Middleware;

public class AdminAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _adminRoles;

    public AdminAuthorizationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        var configuredRoles = configuration.GetSection("Authorization:AdminRoles").Get<string[]>() ?? Array.Empty<string>();
        _adminRoles = configuredRoles
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (_adminRoles.Count == 0)
        {
            _adminRoles.Add("admin");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var roleHeader = context.Request.Headers["X-Role"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(roleHeader) || !_adminRoles.Contains(roleHeader.Trim()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"code\":\"AUTH_FORBIDDEN\",\"msg\":\"需要 admin 權限\"}");
            return;
        }

        await _next(context);
    }
}
