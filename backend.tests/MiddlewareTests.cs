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
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:AdminRoles:0"] = "admin",
                ["Authorization:AdminRoles:1"] = "superadmin"
            })
            .Build();

        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        var middleware = new AdminAuthorizationMiddleware(next, config);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/users";
        context.Request.Headers["X-Role"] = "superadmin";

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    [Fact]
    public async Task AdminAuthorizationMiddleware_RejectsNonAdminRole()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authorization:AdminRoles:0"] = "admin"
            })
            .Build();

        var called = false;
        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        var middleware = new AdminAuthorizationMiddleware(next, config);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/users";
        context.Request.Headers["X-Role"] = "reporter";

        await middleware.InvokeAsync(context);

        Assert.False(called);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }
}
