namespace BankReporting.Api.Models;

public class AuditTrailRecord
{
    public DateTime TimestampUtc { get; set; }
    public string User { get; set; } = "anonymous";
    public string? Role { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public bool IsSensitiveOperation { get; set; }
    public string RiskLevel { get; set; } = "low";
}

public class ComplianceAuditSummary
{
    public int TotalRequests { get; set; }
    public int FailedRequests { get; set; }
    public int SensitiveOperations { get; set; }
    public int HighRiskOperations { get; set; }
    public int UniqueUsers { get; set; }
}

public class ComplianceAuditReportRecord
{
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public ComplianceAuditSummary Summary { get; set; } = new();
    public List<string> TopSensitiveEndpoints { get; set; } = new();
}

public class ComplianceAuditReportsPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ComplianceAuditReportRecord> Reports { get; set; } = new();
}

public class AuditTrailQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<AuditTrailRecord> Records { get; set; } = new();
}

public class AuditBehaviorUserSummary
{
    public string User { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int FailureCount { get; set; }
    public int SensitiveCount { get; set; }
    public long AvgDurationMs { get; set; }
}

public class AuditBehaviorPathSummary
{
    public string PathKey { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public int FailureCount { get; set; }
    public long AvgDurationMs { get; set; }
}

public class AuditBehaviorInsightsPayload
{
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime StartDateUtc { get; set; }
    public DateTime EndDateUtc { get; set; }
    public int TotalRecords { get; set; }
    public int UniqueUsers { get; set; }
    public int SensitiveOperations { get; set; }
    public List<AuditBehaviorUserSummary> TopActiveUsers { get; set; } = new();
    public List<AuditBehaviorPathSummary> TopPaths { get; set; } = new();
    public List<string> OptimizationSuggestions { get; set; } = new();
}

public class AuditTrailTraceStep
{
    public DateTime TimestampUtc { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string RiskLevel { get; set; } = "low";
}

public class AuditTraceNode
{
    public string NodeId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int HitCount { get; set; }
}

public class AuditTraceEdge
{
    public string FromNodeId { get; set; } = string.Empty;
    public string ToNodeId { get; set; } = string.Empty;
    public int TransitionCount { get; set; }
}

public class AuditTrailTraceVisualization
{
    public List<AuditTraceNode> Nodes { get; set; } = new();
    public List<AuditTraceEdge> Edges { get; set; } = new();
    public string MermaidFlowchart { get; set; } = string.Empty;
}

public class AuditTrailTracePayload
{
    public string TraceId { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public DateTime? StartDateUtc { get; set; }
    public DateTime? EndDateUtc { get; set; }
    public int TotalSteps { get; set; }
    public List<AuditTrailTraceStep> Steps { get; set; } = new();
    public AuditTrailTraceVisualization Visualization { get; set; } = new();
    public List<string> ExplainabilityNotes { get; set; } = new();
}

public class AuditDataIntegrityIssue
{
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public string Message { get; set; } = string.Empty;
    public string? RecordRef { get; set; }
}

public class AuditDataIntegrityPayload
{
    public DateTime GeneratedAtUtc { get; set; }
    public int TotalTrailRecords { get; set; }
    public int TotalReportRecords { get; set; }
    public bool IsConsistent { get; set; }
    public int IssueCount { get; set; }
    public List<AuditDataIntegrityIssue> Issues { get; set; } = new();
}

public class RegulationDocumentSnapshot
{
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAtUtc { get; set; }
    public DateTime CapturedAtUtc { get; set; }
    public string? Url { get; set; }
    public List<string> Clauses { get; set; } = new();
}

public class RegulationChangeItem
{
    public string ChangeType { get; set; } = "updated"; // added|removed|updated
    public string ClauseKey { get; set; } = string.Empty;
    public string? PreviousText { get; set; }
    public string? CurrentText { get; set; }
}

public class RegulationImpactArea
{
    public string Domain { get; set; } = string.Empty;
    public string Severity { get; set; } = "low"; // low|medium|high
    public string Reason { get; set; } = string.Empty;
}

public class RegulationImpactAnalysisRecord
{
    public string AnalysisId { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime GeneratedAtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
    public string DocumentCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime BaselinePublishedAtUtc { get; set; }
    public DateTime CurrentPublishedAtUtc { get; set; }
    public List<RegulationChangeItem> Changes { get; set; } = new();
    public List<RegulationImpactArea> ImpactAreas { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

public class RegulationImpactQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<RegulationImpactAnalysisRecord> Records { get; set; } = new();
}

public class ExternalComplianceProviderConfig
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string FetchPath { get; set; } = "/api/v1/risk-list";
    public string? ApiKey { get; set; }
    public bool Enabled { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
}

public class ExternalRiskRecord
{
    public string RecordId { get; set; } = Guid.NewGuid().ToString("N");
    public string ProviderName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = "sanctions";
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string RiskLevel { get; set; } = "medium";
    public List<string> Tags { get; set; } = new();
    public DateTime ImportedAtUtc { get; set; }
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public Dictionary<string, string> Raw { get; set; } = new();
}

public class ExternalRiskDataSyncResult
{
    public string ProviderName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = string.Empty;
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public DateTime SyncedAtUtc { get; set; }
}

public class ExternalRiskMatchItem
{
    public string RecordId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string DatasetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string RiskLevel { get; set; } = "medium";
    public double Score { get; set; }
    public List<string> Tags { get; set; } = new();
}

public class ExternalRiskScreeningResult
{
    public string CustomerName { get; set; } = string.Empty;
    public string? Country { get; set; }
    public int TotalMatches { get; set; }
    public string SuggestedDecision { get; set; } = "clear";
    public List<ExternalRiskMatchItem> Matches { get; set; } = new();
}

public class FinancialMarketSnapshot
{
    public string SnapshotId { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceName { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
    public double VolatilityIndex { get; set; }
    public double CreditSpreadBps { get; set; }
    public double FxVolatilityPercent { get; set; }
    public string LiquidityStressLevel { get; set; } = "low"; // low|medium|high
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public class FinancialMarketSnapshotQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<FinancialMarketSnapshot> Records { get; set; } = new();
}

public class ComplianceAlertRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RuleType { get; set; } = "failed_requests";
    public bool Enabled { get; set; } = true;
    public string Severity { get; set; } = "medium";
    public int Threshold { get; set; } = 1;
    public int WindowMinutes { get; set; } = 15;
    public string? RiskLevel { get; set; }
    public bool SensitiveOnly { get; set; }
    public int MinErrorRatePercent { get; set; }
    public int MinDistinctPaths { get; set; } = 1;
    public int CooldownMinutes { get; set; }
    public List<string> ExcludedPaths { get; set; } = new();
    public DateTime UpdatedAtUtc { get; set; }
}

public class ComplianceAlertRulesPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ComplianceAlertRule> Rules { get; set; } = new();
}

public class ComplianceAlertExplainability
{
    public List<string> TriggerReasons { get; set; } = new();
    public Dictionary<string, string> Metrics { get; set; } = new();
    public List<string> EvidenceSamples { get; set; } = new();
}

public class ComplianceAlertRecord
{
    public string AlertId { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RuleName { get; set; } = string.Empty;
    public string Severity { get; set; } = "medium";
    public int SeverityScore { get; set; }
    public DateTime TriggeredAtUtc { get; set; }
    public int WindowMinutes { get; set; }
    public int TriggerCount { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public List<string> TriggerDetails { get; set; } = new();
    public List<string> NotifyChannels { get; set; } = new();
    public ComplianceAlertExplainability Explainability { get; set; } = new();
}

public class ComplianceAlertEvaluateResult
{
    public DateTime EvaluatedAtUtc { get; set; }
    public int EvaluatedRules { get; set; }
    public int TriggeredAlerts { get; set; }
    public List<ComplianceAlertRecord> Alerts { get; set; } = new();
}

public class ComplianceAlertQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<ComplianceAlertRecord> Alerts { get; set; } = new();
}

public class PredictiveComplianceRiskFactor
{
    public string FactorKey { get; set; } = string.Empty;
    public string FactorName { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Evidence { get; set; } = string.Empty;
}

public class PredictiveComplianceRiskReport
{
    public string AssessmentId { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public int LookbackDays { get; set; }
    public int ForecastDays { get; set; }
    public string PredictedRiskLevel { get; set; } = "low";
    public int RiskScore { get; set; }
    public int ConfidenceScore { get; set; }
    public List<PredictiveComplianceRiskFactor> Factors { get; set; } = new();
    public List<string> EarlyWarnings { get; set; } = new();
    public List<string> RecommendedActions { get; set; } = new();
}

public class PredictiveComplianceRiskQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<PredictiveComplianceRiskReport> Reports { get; set; } = new();
}

public class BlockchainAuditAnchorRecord
{
    public string AnchorId { get; set; } = string.Empty;
    public string AnchorType { get; set; } = "audit_trail";
    public string Network { get; set; } = "sandbox-ledger";
    public string Summary { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string AnchorHash { get; set; } = string.Empty;
    public string? PreviousAnchorHash { get; set; }
    public string SuggestedVerification { get; set; } = string.Empty;
    public List<string> AuditTrailIds { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
}

public class BlockchainAuditAnchorQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<BlockchainAuditAnchorRecord> Records { get; set; } = new();
}

public class BlockchainDataSharingSimulationResult
{
    public string PackageId { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public List<string> Participants { get; set; } = new();
    public List<string> Fields { get; set; } = new();
    public string RecommendedMode { get; set; } = "proof-with-hash-pointer";
    public List<string> PolicyViolations { get; set; } = new();
    public List<string> SuggestedNextActions { get; set; } = new();
}

public class IntelligentReportSubmissionRecord
{
    public string AutomationId { get; set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; set; }
    public string BankCode { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string ReportYear { get; set; } = string.Empty;
    public string ReportMonth { get; set; } = string.Empty;
    public string ReportId { get; set; } = string.Empty;
    public string Status { get; set; } = "generated"; // generated|submitted|failed|dry-run
    public bool DryRun { get; set; }
    public object StandardizedReport { get; set; } = new();
    public string? RequestId { get; set; }
    public string? SubmissionCode { get; set; }
    public string? SubmissionMessage { get; set; }
    public List<string> ValidationWarnings { get; set; } = new();
}

public class IntelligentReportSubmissionQueryPayload
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<IntelligentReportSubmissionRecord> Records { get; set; } = new();
}
