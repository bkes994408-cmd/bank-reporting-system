using System.Security.Cryptography;
using System.Text;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IBlockchainAdapterService
{
    Task<BlockchainAnchorResult> AnchorAsync(BlockchainAnchorRequest request);
}

/// <summary>
/// 模擬上鏈 adapter（可替換為實鏈）
/// </summary>
public class SimulatedBlockchainAdapterService : IBlockchainAdapterService
{
    public Task<BlockchainAnchorResult> AnchorAsync(BlockchainAnchorRequest request)
    {
        var seeded = $"{request.Network}|{request.DataDigest}|{request.CorrelationId}|{DateTimeOffset.UtcNow:O}";
        var fullHash = ComputeHash(seeded);

        var result = new BlockchainAnchorResult
        {
            Network = request.Network,
            AdapterName = nameof(SimulatedBlockchainAdapterService),
            TransactionId = $"0x{fullHash[..32]}",
            BlockId = $"BLK-{fullHash[32..48]}",
            AnchoredAt = DateTimeOffset.UtcNow,
            AnchorHash = fullHash
        };

        return Task.FromResult(result);
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
