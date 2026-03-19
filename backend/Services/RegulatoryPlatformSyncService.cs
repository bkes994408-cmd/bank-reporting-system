using BankReporting.Api.DTOs;
using BankReporting.Api.Models;

namespace BankReporting.Api.Services;

public interface IRegulatoryPlatformSyncService
{
    Task<ApiResponse<RegulatoryAuditReportSyncResult>> GenerateAndSyncAuditReportAsync(RegulatoryAuditReportSyncRequest request, CancellationToken cancellationToken);
}

public class RegulatoryPlatformSyncService : IRegulatoryPlatformSyncService
{
    private readonly IComplianceAuditService _complianceAuditService;
    private readonly IThirdPartyIntegrationService _thirdPartyIntegrationService;

    public RegulatoryPlatformSyncService(
        IComplianceAuditService complianceAuditService,
        IThirdPartyIntegrationService thirdPartyIntegrationService)
    {
        _complianceAuditService = complianceAuditService;
        _thirdPartyIntegrationService = thirdPartyIntegrationService;
    }

    public async Task<ApiResponse<RegulatoryAuditReportSyncResult>> GenerateAndSyncAuditReportAsync(
        RegulatoryAuditReportSyncRequest request,
        CancellationToken cancellationToken)
    {
        var auditReport = await _complianceAuditService.GenerateReportAsync(new ComplianceAuditReportGenerateRequest
        {
            StartDateUtc = request.StartDateUtc,
            EndDateUtc = request.EndDateUtc
        }, cancellationToken);

        var period = $"{auditReport.StartDateUtc:yyyy-MM-dd}~{auditReport.EndDateUtc:yyyy-MM-dd}";
        var syncResponse = await _thirdPartyIntegrationService.SyncAsync(new ThirdPartySyncRequest
        {
            SystemName = request.PlatformSystemName,
            EventType = "compliance.audit_report",
            BankCode = request.BankCode,
            ReportId = auditReport.ReportId,
            Period = period,
            Status = "generated",
            RequestId = $"audit-{auditReport.ReportId}",
            Data = new
            {
                auditReport.ReportId,
                auditReport.GeneratedAtUtc,
                auditReport.StartDateUtc,
                auditReport.EndDateUtc,
                auditReport.Summary,
                auditReport.TopSensitiveEndpoints
            }
        });

        return new ApiResponse<RegulatoryAuditReportSyncResult>
        {
            Code = syncResponse.Code,
            Msg = syncResponse.Code == "0000"
                ? "合規審計報告生成並同步監管平台成功"
                : $"合規審計報告已生成，但同步失敗：{syncResponse.Msg}",
            Payload = new RegulatoryAuditReportSyncResult
            {
                BankCode = request.BankCode,
                PlatformSystemName = request.PlatformSystemName,
                AuditReport = auditReport,
                SyncResult = syncResponse.Payload ?? new ThirdPartySyncResult
                {
                    SystemName = request.PlatformSystemName,
                    Success = false,
                    Message = syncResponse.Msg
                },
                Synced = syncResponse.Code == "0000"
            }
        };
    }
}
