using BankReporting.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "銀行監理資料數位申報系統 API", Version = "v1" });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// Register services
builder.Services.AddSingleton<IAgentService, AgentService>();
builder.Services.AddHttpClient<IAgentService, AgentService>();
builder.Services.AddSingleton<IMonitoringService, MonitoringService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("RequestMonitoring");
    var monitoringService = context.RequestServices.GetRequiredService<IMonitoringService>();

    var startedAt = DateTime.UtcNow;
    try
    {
        await next();
    }
    finally
    {
        var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
        var path = context.Request.Path.HasValue ? context.Request.Path.Value! : "/";
        monitoringService.RecordRequest(context.Request.Method, path, context.Response.StatusCode, durationMs);

        logger.LogInformation("HTTP {Method} {Path} => {StatusCode} ({DurationMs}ms)",
            context.Request.Method,
            path,
            context.Response.StatusCode,
            durationMs);

        if (context.Response.StatusCode >= 500)
        {
            logger.LogWarning("ALERT: High-severity API error detected. {Method} {Path} => {StatusCode}",
                context.Request.Method,
                path,
                context.Response.StatusCode);
        }

        if (durationMs >= 2000)
        {
            logger.LogWarning("ALERT: Slow API response detected. {Method} {Path} took {DurationMs}ms",
                context.Request.Method,
                path,
                durationMs);
        }
    }
});

app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
