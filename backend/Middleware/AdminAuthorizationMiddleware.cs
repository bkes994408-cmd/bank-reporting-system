namespace BankReporting.Api.Middleware;

public class AdminAuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HashSet<string> _adminRoles;
    private readonly HashSet<string> _operatorRoles;

    public AdminAuthorizationMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;

        var configuredAdminRoles = configuration.GetSection("Authorization:AdminRoles").Get<string[]>() ?? Array.Empty<string>();
        _adminRoles = ToRoleSet(configuredAdminRoles, "admin", "superadmin");

        var configuredOperatorRoles = configuration.GetSection("Authorization:OperatorRoles").Get<string[]>() ?? Array.Empty<string>();
        _operatorRoles = ToRoleSet(configuredOperatorRoles, "admin", "superadmin", "reporter");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        var method = context.Request.Method;

        if (path.StartsWith("/api/admin", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsInRole(context, _adminRoles))
            {
                await ForbidAsync(context, "需要 admin 權限");
                return;
            }
        }
        else if (RequiresOperatorRole(path, method))
        {
            if (!IsInRole(context, _operatorRoles))
            {
                await ForbidAsync(context, "需要 operator 權限（admin/reporter）");
                return;
            }
        }

        await _next(context);
    }

    private static bool RequiresOperatorRole(string path, string method)
    {
        if (path.StartsWith("/api/keys", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(method))
        {
            return true;
        }

        if (path.Equals("/api/token/update", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(method))
        {
            return true;
        }

        return false;
    }

    private static bool IsInRole(HttpContext context, HashSet<string> allowedRoles)
    {
        var roleHeader = context.Request.Headers["X-Role"].FirstOrDefault();
        return !string.IsNullOrWhiteSpace(roleHeader) && allowedRoles.Contains(roleHeader.Trim());
    }

    private static async Task ForbidAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync($"{{\"code\":\"AUTH_FORBIDDEN\",\"msg\":\"{message}\"}}");
    }

    private static HashSet<string> ToRoleSet(IEnumerable<string> configured, params string[] defaults)
    {
        var set = configured
            .Select(x => x?.Trim() ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (set.Count == 0)
        {
            foreach (var role in defaults)
            {
                set.Add(role);
            }
        }

        return set;
    }
}
