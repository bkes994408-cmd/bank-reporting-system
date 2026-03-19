using BankReporting.Api.Middleware;
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
builder.Services.AddSingleton<IExcelParsingService, ExcelParsingService>();
builder.Services.AddHttpClient<IAgentService, AgentService>((sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["AgentSettings:BaseUrl"] ?? "https://127.0.0.1:8005/APBSA";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddSingleton<IMonitoringService, MonitoringService>();
builder.Services.AddSingleton<IAdAuthService, AdAuthService>();
builder.Services.AddSingleton<IAdminService, AdminService>();
builder.Services.AddSingleton<IThirdPartyIntegrationService, ThirdPartyIntegrationService>();
builder.Services.AddSingleton<IReportHistoryArchiveService, ReportHistoryArchiveService>();
builder.Services.AddSingleton<IComplianceAuditService, ComplianceAuditService>();
builder.Services.AddSingleton<IRegulationMonitoringService, RegulationMonitoringService>();
builder.Services.AddHttpClient(nameof(ExternalComplianceDataService));
builder.Services.AddSingleton<IExternalComplianceDataService, ExternalComplianceDataService>();
builder.Services.AddSingleton<IComplianceAlertService, ComplianceAlertService>();
builder.Services.AddSingleton<IFinancialMarketDataService, FinancialMarketDataService>();
builder.Services.AddSingleton<IPredictiveComplianceRiskService, PredictiveComplianceRiskService>();
builder.Services.AddSingleton<IBlockchainComplianceService, BlockchainComplianceService>();
builder.Services.AddSingleton<IBlockchainAdapterService, SimulatedBlockchainAdapterService>();
builder.Services.AddSingleton<IComplianceProofPersistence, FileComplianceProofPersistence>();
builder.Services.AddSingleton<IComplianceProofService, ComplianceProofService>();
builder.Services.AddSingleton<IIntelligentReportAutomationService, IntelligentReportAutomationService>();
builder.Services.AddSingleton<IEncryptedExportArchiveService, EncryptedExportArchiveService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var disableHttpsRedirection = app.Configuration.GetValue<bool>("DISABLE_HTTPS_REDIRECTION");
if (!app.Environment.IsEnvironment("Test") && !disableHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");
app.UseMiddleware<AdminAuthorizationMiddleware>();
app.UseMiddleware<RequestMonitoringMiddleware>();

app.UseAuthorization();

// Basic health check endpoint for load balancers / monitoring
app.MapGet("/health", () => Results.Text("ok", "text/plain"));

app.MapControllers();

app.Run();

public partial class Program { }
