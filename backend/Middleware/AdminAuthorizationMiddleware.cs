namespace BankReporting.Api.Middleware;

public class AdminAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public AdminAuthorizationMiddleware(RequestDelegate next)
    {
        _next = next;
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
        if (!string.Equals(roleHeader, "admin", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"code\":\"AUTH_FORBIDDEN\",\"msg\":\"需要 admin 權限\"}");
            return;
        }

        await _next(context);
    }
}
