using System.Text.Json;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IComplianceProofPersistence
{
    ComplianceProofStoreSnapshot Load();
    void Save(ComplianceProofStoreSnapshot snapshot);
}

public class ComplianceProofStoreSnapshot
{
    public Dictionary<string, ComplianceProof> Proofs { get; set; } = new();
    public Dictionary<string, string> TransactionToProofId { get; set; } = new();
    public Dictionary<string, List<AuditTrailEntry>> AuditTrailByCorrelationId { get; set; } = new();
    public Dictionary<string, string> IdempotencyKeyToProofId { get; set; } = new();
}

public class FileComplianceProofPersistence : IComplianceProofPersistence
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public FileComplianceProofPersistence(IHostEnvironment environment)
    {
        _filePath = Path.Combine(environment.ContentRootPath, "App_Data", "compliance-proof-store.json");
    }

    public ComplianceProofStoreSnapshot Load()
    {
        if (!File.Exists(_filePath))
        {
            return new ComplianceProofStoreSnapshot();
        }

        var json = File.ReadAllText(_filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ComplianceProofStoreSnapshot();
        }

        return JsonSerializer.Deserialize<ComplianceProofStoreSnapshot>(json) ?? new ComplianceProofStoreSnapshot();
    }

    public void Save(ComplianceProofStoreSnapshot snapshot)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
