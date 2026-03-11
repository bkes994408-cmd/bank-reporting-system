using System.Collections.Concurrent;
using System.Text.Json;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IExternalComplianceDataService
{
    Task<ExternalRiskDataSyncResult> SyncRiskDataAsync(ExternalRiskDataSyncRequest request, CancellationToken cancellationToken);
    ExternalRiskScreeningResult ScreenRisk(ExternalRiskScreeningRequest request);
}

public class ExternalComplianceDataService : IExternalComplianceDataService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly List<ExternalComplianceProviderConfig> _providers;
    private readonly ConcurrentQueue<ExternalRiskRecord> _records = new();

    public ExternalComplianceDataService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _providers = configuration.GetSection("ExternalComplianceData:Providers")
            .Get<List<ExternalComplianceProviderConfig>>() ?? new List<ExternalComplianceProviderConfig>();
    }

    public async Task<ExternalRiskDataSyncResult> SyncRiskDataAsync(ExternalRiskDataSyncRequest request, CancellationToken cancellationToken)
    {
        var providerName = request.ProviderName.Trim();
        var datasetType = request.DatasetType.Trim().ToLowerInvariant();

        var provider = _providers.FirstOrDefault(x =>
            string.Equals(x.Name, providerName, StringComparison.OrdinalIgnoreCase) && x.Enabled);

        if (provider is null)
        {
            throw new InvalidOperationException("指定的外部合規數據源不存在或未啟用");
        }

        var path = string.IsNullOrWhiteSpace(request.PathOverride) ? provider.FetchPath : request.PathOverride!.Trim();
        var baseUrl = provider.BaseUrl.EndsWith('/') ? provider.BaseUrl : provider.BaseUrl + "/";
        var url = new Uri(new Uri(baseUrl), path.TrimStart('/'));

        var client = _httpClientFactory.CreateClient(nameof(ExternalComplianceDataService));
        client.Timeout = TimeSpan.FromSeconds(Math.Clamp(provider.TimeoutSeconds, 3, 60));

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-API-Key", provider.ApiKey);
        }

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var sourceItems = ParseSourceItems(body);
        var fieldMappings = ToCaseInsensitiveMappings(request.FieldMappings);

        var imported = 0;
        var skipped = 0;

        foreach (var item in sourceItems)
        {
            var normalized = Normalize(item, fieldMappings);
            if (string.IsNullOrWhiteSpace(normalized.Name))
            {
                skipped++;
                continue;
            }

            normalized.ProviderName = provider.Name;
            normalized.DatasetType = datasetType;
            normalized.ImportedAtUtc = DateTime.UtcNow;
            normalized.RecordId = $"risk-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];

            _records.Enqueue(normalized);
            imported++;
        }

        while (_records.Count > 10000 && _records.TryDequeue(out _))
        {
        }

        return new ExternalRiskDataSyncResult
        {
            ProviderName = provider.Name,
            DatasetType = datasetType,
            ImportedCount = imported,
            SkippedCount = skipped,
            SyncedAtUtc = DateTime.UtcNow
        };
    }

    public ExternalRiskScreeningResult ScreenRisk(ExternalRiskScreeningRequest request)
    {
        var name = request.CustomerName.Trim();
        var country = request.Country?.Trim();
        var datasetType = request.DatasetType?.Trim();

        var query = _records.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(datasetType))
        {
            query = query.Where(x => string.Equals(x.DatasetType, datasetType, StringComparison.OrdinalIgnoreCase));
        }

        var matches = query
            .Select(x => new { Record = x, Score = ComputeNameScore(name, x.Name) })
            .Where(x => x.Score >= 0.7)
            .Select(x => new ExternalRiskMatchItem
            {
                RecordId = x.Record.RecordId,
                ProviderName = x.Record.ProviderName,
                DatasetType = x.Record.DatasetType,
                Name = x.Record.Name,
                Country = x.Record.Country,
                RiskLevel = x.Record.RiskLevel,
                Score = x.Score,
                Tags = x.Record.Tags
            })
            .OrderByDescending(x => x.Score)
            .Take(20)
            .ToList();

        if (!string.IsNullOrWhiteSpace(country))
        {
            foreach (var match in matches)
            {
                if (!string.IsNullOrWhiteSpace(match.Country)
                    && string.Equals(match.Country, country, StringComparison.OrdinalIgnoreCase))
                {
                    match.Score = Math.Min(1, match.Score + 0.1);
                }
            }

            matches = matches.OrderByDescending(x => x.Score).ToList();
        }

        return new ExternalRiskScreeningResult
        {
            CustomerName = name,
            Country = country,
            TotalMatches = matches.Count,
            SuggestedDecision = SuggestDecision(matches),
            Matches = matches
        };
    }

    private static List<Dictionary<string, string>> ParseSourceItems(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(ToFlatDictionary).ToList();
        }

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return items.EnumerateArray().Select(ToFlatDictionary).ToList();
        }

        throw new InvalidOperationException("外部數據格式不正確，預期為 JSON array 或 { items: [] }");
    }

    private static Dictionary<string, string> ToFlatDictionary(JsonElement element)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (var p in element.EnumerateObject())
        {
            dict[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString() ?? string.Empty,
                JsonValueKind.Number => p.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => p.Value.GetRawText()
            };
        }

        return dict;
    }

    private static ExternalRiskRecord Normalize(Dictionary<string, string> source, Dictionary<string, string> fieldMappings)
    {
        string Get(string key, params string[] aliases)
        {
            if (fieldMappings.TryGetValue(key, out var mapped) && source.TryGetValue(mapped, out var mappedValue))
            {
                return mappedValue.Trim();
            }

            foreach (var k in aliases.Prepend(key))
            {
                if (source.TryGetValue(k, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        var riskLevel = Get("riskLevel", "risk_level", "severity").ToLowerInvariant();
        if (riskLevel is not ("low" or "medium" or "high"))
        {
            riskLevel = "medium";
        }

        var tagsRaw = Get("tags", "tag_list");
        var tags = tagsRaw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DateTime? sourceUpdatedAtUtc = null;
        if (DateTime.TryParse(Get("updatedAt", "updated_at"), out var parsed))
        {
            sourceUpdatedAtUtc = parsed.ToUniversalTime();
        }

        return new ExternalRiskRecord
        {
            ExternalId = Get("externalId", "id", "entity_id"),
            Name = Get("name", "full_name", "entity_name"),
            Country = NullIfEmpty(Get("country", "jurisdiction")?.ToUpperInvariant()),
            RiskLevel = riskLevel,
            Tags = tags,
            SourceUpdatedAtUtc = sourceUpdatedAtUtc,
            Raw = source.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static Dictionary<string, string> ToCaseInsensitiveMappings(Dictionary<string, string>? fieldMappings)
    {
        if (fieldMappings is null || fieldMappings.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in fieldMappings)
        {
            var key = mapping.Key?.Trim();
            var value = mapping.Value?.Trim();
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized[key] = value;
        }

        return normalized;
    }

    private static string SuggestDecision(List<ExternalRiskMatchItem> matches)
    {
        if (matches.Any(x => x.RiskLevel == "high" && x.Score >= 0.9))
        {
            return "block";
        }

        if (matches.Any(x => x.Score >= 0.8))
        {
            return "review";
        }

        return matches.Count == 0 ? "clear" : "monitor";
    }

    private static double ComputeNameScore(string target, string candidate)
    {
        if (string.Equals(target, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (candidate.Contains(target, StringComparison.OrdinalIgnoreCase)
            || target.Contains(candidate, StringComparison.OrdinalIgnoreCase))
        {
            return 0.85;
        }

        var targetTokens = target.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var candidateTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (targetTokens.Length == 0 || candidateTokens.Length == 0)
        {
            return 0;
        }

        var overlap = targetTokens.Intersect(candidateTokens, StringComparer.OrdinalIgnoreCase).Count();
        return (double)overlap / Math.Max(targetTokens.Length, candidateTokens.Length);
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
