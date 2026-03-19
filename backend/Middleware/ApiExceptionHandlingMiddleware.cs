using System.Text.Json;
using BankReporting.Api.Models;

namespace BankReporting.Api.Middleware;

public class ApiExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionHandlingMiddleware> _logger;

    public ApiExceptionHandlingMiddleware(RequestDelegate next, ILogger<ApiExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Business validation failed: {Path}", context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "API_4000", ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            _logger.LogWarning(ex, "Bad request payload: {Path}", context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "API_4001", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled API exception: {Path}", context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError, "API_5000", "系統發生未預期錯誤，請稍後再試");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string code, string message)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new ApiResponse<object>
        {
            Code = code,
            Msg = message,
            Payload = null
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }
}
