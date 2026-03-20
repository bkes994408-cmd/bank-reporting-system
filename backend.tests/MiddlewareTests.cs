using System.Text;
using BankReporting.Api.Middleware;
using BankReporting.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
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

    [Fact]
    public async Task AdminAuthorizationMiddleware_AllowsReporterOnDeclareRoute()
    {
        var middleware = BuildMiddleware(out var called);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/declare";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-Role"] = "reporter";

        await middleware.InvokeAsync(context);

        Assert.True(called());
    }

    [Fact]
    public async Task AdminAuthorizationMiddleware_RejectsViewerOnDeclareRoute()
    {
        var middleware = BuildMiddleware(out var called);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/declare";
        context.Request.Method = HttpMethods.Post;
        context.Request.Headers["X-Role"] = "viewer";

        await middleware.InvokeAsync(context);

        Assert.False(called());
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApiExceptionHandlingMiddleware_ReturnsStandardizedErrorResponse()
    {
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw new Exception("boom"),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("\"code\":\"API_5000\"", content);
        Assert.Contains("\"payload\":null", content);
    }

    [Fact]
    public async Task ApiExceptionHandlingMiddleware_MapsTimeoutException_To504()
    {
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw new TimeoutException("slow"),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, context.Response.StatusCode);
        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("\"code\":\"API_5040\"", content);
    }

    [Fact]
    public async Task ApiExceptionHandlingMiddleware_MapsArgumentException_To400()
    {
        var middleware = new ApiExceptionHandlingMiddleware(
            _ => throw new ArgumentException("invalid range"),
            NullLogger<ApiExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        context.Response.Body.Position = 0;

        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8);
        var content = await reader.ReadToEndAsync();
        Assert.Contains("\"code\":\"API_4003\"", content);
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
