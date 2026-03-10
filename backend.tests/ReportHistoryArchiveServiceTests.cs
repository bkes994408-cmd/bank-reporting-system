using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Moq;
using Xunit;

namespace BankReporting.Tests;

public class ReportHistoryArchiveServiceTests
{
    [Fact]
    public async Task ArchiveAsync_ReplacesSameKeyDataset()
    {
        var mockAgentService = new Mock<IAgentService>();
        mockAgentService
            .SetupSequence(x => x.GetReportHistoriesAsync(It.IsAny<ReportHistoriesRequest>()))
            .ReturnsAsync(new ApiResponse<ReportHistoriesPayload>
            {
                Code = "0000",
                Payload = new ReportHistoriesPayload
                {
                    Reports = new List<ReportHistory>
                    {
                        new() { RequestId = "A", Status = "SUCCESS" },
                        new() { RequestId = "B", Status = "FAILED" }
                    }
                }
            })
            .ReturnsAsync(new ApiResponse<ReportHistoriesPayload>
            {
                Code = "0000",
                Payload = new ReportHistoriesPayload
                {
                    Reports = new List<ReportHistory>
                    {
                        new() { RequestId = "C", Status = "SUCCESS" }
                    }
                }
            });

        var service = new ReportHistoryArchiveService(mockAgentService.Object);
        var request = new ReportHistoriesRequest { BankCode = "0070000", ReportId = "AI330", Year = "113", Type = "monthly" };

        await service.ArchiveAsync(request);
        await service.ArchiveAsync(request);

        var query = service.Query(new ArchivedReportHistoriesQueryRequest { BankCode = "0070000", ReportId = "AI330", Year = "113" });

        Assert.Equal(1, query.Total);
        Assert.Single(query.Reports);
        Assert.Equal("C", query.Reports[0].Report.RequestId);
    }

    [Fact]
    public async Task Query_CanFilterByStatusAndPagination()
    {
        var mockAgentService = new Mock<IAgentService>();
        mockAgentService
            .Setup(x => x.GetReportHistoriesAsync(It.IsAny<ReportHistoriesRequest>()))
            .ReturnsAsync(new ApiResponse<ReportHistoriesPayload>
            {
                Code = "0000",
                Payload = new ReportHistoriesPayload
                {
                    Reports = new List<ReportHistory>
                    {
                        new() { RequestId = "A", Status = "SUCCESS" },
                        new() { RequestId = "B", Status = "FAILED" },
                        new() { RequestId = "C", Status = "SUCCESS" }
                    }
                }
            });

        var service = new ReportHistoryArchiveService(mockAgentService.Object);
        await service.ArchiveAsync(new ReportHistoriesRequest { BankCode = "0070000", ReportId = "AI330", Year = "113" });

        var query = service.Query(new ArchivedReportHistoriesQueryRequest
        {
            BankCode = "0070000",
            Status = "SUCCESS",
            Page = 1,
            PageSize = 1
        });

        Assert.Equal(2, query.Total);
        Assert.Single(query.Reports);
    }
}
