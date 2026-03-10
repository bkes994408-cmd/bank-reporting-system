using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace BankReporting.Tests;

public class EncryptedExportArchiveServiceTests
{
    [Fact]
    public async Task ArchiveDeclareResultAsync_ShouldStoreEncryptedPayloadAndMaskedIds()
    {
        var mockAgentService = new Mock<IAgentService>();
        mockAgentService
            .Setup(x => x.GetDeclareResultAsync(It.IsAny<DeclareResultRequest>()))
            .ReturnsAsync(new ApiResponse<ReportDeclarationResult>
            {
                Code = "0000",
                Msg = "ok",
                Payload = new ReportDeclarationResult
                {
                    BankCode = "0070000",
                    ReportId = "AI330",
                    Year = "113",
                    RequestId = "REQ123456",
                    TransactionId = "TX987654"
                }
            });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EncryptionArchive:Passphrase"] = "test-passphrase"
            })
            .Build();

        var service = new EncryptedExportArchiveService(mockAgentService.Object, config);

        var record = await service.ArchiveDeclareResultAsync(new DeclareResultRequest
        {
            RequestId = "REQ123456",
            TransactionId = "TX987654"
        });

        Assert.Equal("declare-result", record.Category);
        Assert.Equal("RE***56", record.RequestIdMasked);
        Assert.Equal("TX***54", record.TransactionIdMasked);
        Assert.NotEmpty(record.CipherTextBase64);
        Assert.NotEmpty(record.TagBase64);
        Assert.NotEmpty(record.NonceBase64);
        Assert.Equal(64, record.DataSha256Hex.Length);
    }

    [Fact]
    public async Task Query_ShouldFilterByMaskedRequestId()
    {
        var mockAgentService = new Mock<IAgentService>();
        mockAgentService
            .Setup(x => x.GetDeclareResultAsync(It.IsAny<DeclareResultRequest>()))
            .ReturnsAsync(new ApiResponse<ReportDeclarationResult>
            {
                Code = "0000",
                Msg = "ok",
                Payload = new ReportDeclarationResult { BankCode = "0070000", ReportId = "AI330", Year = "113" }
            });

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EncryptionArchive:Passphrase"] = "test-passphrase"
        }).Build();

        var service = new EncryptedExportArchiveService(mockAgentService.Object, config);
        await service.ArchiveDeclareResultAsync(new DeclareResultRequest { RequestId = "REQ123456" });
        await service.ArchiveDeclareResultAsync(new DeclareResultRequest { RequestId = "ABCD9999" });

        var result = service.Query(new EncryptedArchiveQueryRequest { RequestId = "REQ123456" });

        Assert.Equal(1, result.Total);
        Assert.Equal("RE***56", result.Records[0].RequestIdMasked);
    }
}
