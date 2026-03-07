using BankReporting.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BankReporting.Tests;

public class MiddlewareTests
{
    [Fact]
    public async Task AdminAuthorizationMiddleware_AllowsConfiguredRole()
    {
        var middleware = BuildMiddleware(out var called);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/users";
        context.Request.Headers["X-Role"] = "superadmin";

        await middleware.InvokeAsync(context);

        Assert.True(called());
    }

    [Fact]
    public async Task AdminAuthorizationMiddleware_RejectsNonAdminRole()
    {
        var middleware = BuildMiddleware(out var called);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/users";
        context.Request.Headers["X-Role"] = "reporter";

        await middleware.InvokeAsync(context);

        Assert.False(called());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task AdminAuthorizationMiddleware_AllowsReporterOnOperatorRoute()
    {
        var middleware = BuildMiddleware(out var called);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/token/update";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-Role"] = "reporter";

        await middleware.InvokeAsync(context);

        Assert.True(called());
    }

    [Fact]
    public async Task AdminAuthorizationMiddleware_RejectsViewerOnOperatorRoute()
    {
        var middleware = BuildMiddleware(out var called);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/keys/import";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-Role"] = "viewer";

        await middleware.InvokeAsync(context);

        Assert.False(called());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    private static AdminAuthorizationMiddleware BuildMiddleware(out Func<bool> called)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:AdminRoles:0"] = "admin",
                ["Authorization:AdminRoles:1"] = "superadmin",
                ["Authorization:OperatorRoles:0"] = "admin",
                ["Authorization:OperatorRoles:1"] = "superadmin",
                ["Authorization:OperatorRoles:2"] = "reporter"
            })
            .Build();

        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        called = () => nextCalled;
        return new AdminAuthorizationMiddleware(next, config);
    }
}
