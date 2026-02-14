using BankReporting.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;

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

// Reverse-proxy support (X-Forwarded-For / X-Forwarded-Proto)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Allow all proxies/networks by default (common for container deployments).
    // Lock this down if you have a fixed proxy IP.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// HTTPS redirect can be controlled explicitly via config/env var.
// Default: enabled for non-Development, disabled for Development.
var enableHttpsRedirect = builder.Configuration.GetValue<bool?>("ENABLE_HTTPS_REDIRECT")
                        ?? !builder.Environment.IsDevelopment();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseForwardedHeaders();

if (enableHttpsRedirect)
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program { }
