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
builder.Services.AddSingleton<IAgentService, AgentService>();
builder.Services.AddHttpClient<IAgentService, AgentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Test"))
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");
app.UseAuthorization();

// Basic health check endpoint for load balancers / monitoring
app.MapGet("/health", () =>
{
    var version = builder.Configuration["AppVersion"] ?? "1.0.0";
    return Results.Ok(new { status = "ok", version });
});

app.MapControllers();

app.Run();

public partial class Program { }
