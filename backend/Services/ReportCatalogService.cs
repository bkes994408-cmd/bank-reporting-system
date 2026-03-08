using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IReportCatalogService
{
    IReadOnlyList<ReportCatalogItem> GetAll();
}

public class ReportCatalogService : IReportCatalogService
{
    private readonly IConfiguration _configuration;

    public ReportCatalogService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IReadOnlyList<ReportCatalogItem> GetAll()
    {
        var configured = _configuration.GetSection("ReportCatalog:Items").Get<List<ReportCatalogItem>>();
        var source = configured is { Count: > 0 }
            ? configured
            : ReportTypes.DefaultCatalog;

        return source
            .Where(x => !string.IsNullOrWhiteSpace(x.Id))
            .Select(x => new ReportCatalogItem
            {
                Id = x.Id.Trim(),
                Name = string.IsNullOrWhiteSpace(x.Name) ? x.Id.Trim() : x.Name.Trim()
            })
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
