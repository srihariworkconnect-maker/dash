# V2 changed-file index

Paths are repository-relative. Baseline: 7ccf470. Gate F includes integration refinements to earlier modules. No original solution files were deleted.

## db9d592 Gate A: preserve normalized ADO evidence and reconciliation state

- `AGENTS.md`
- `Sandpiper.QA.Dashboard.Server/Models/Analytics.cs`
- `Sandpiper.QA.Dashboard.Server/Models/NormalizedPoint.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/TestPointNormalizer.cs`
- `Sandpiper.QA.Dashboard.Server/Services/FrontierAnalyticsServices.cs`
- `docs/v2-gates.md`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/NormalizationTests.cs`

## 54afb08 Gate B: add shared command-center shell, explorer and live TCM

- `Sandpiper.QA.Dashboard.Server/Models/Analytics.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ReportingClock.cs`
- `Sandpiper.QA.Dashboard.Server/Services/FrontierAnalyticsServices.cs`
- `docs/v2-gates.md`
- `sandpiper.qa.dashboard.client/src/App.tsx`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/app/ReportingContext.ts`
- `sandpiper.qa.dashboard.client/src/components/DataTable.tsx`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `sandpiper.qa.dashboard.client/src/v2.css`
- `sandpiper.qa.dashboard.client/tests/reporting-context.test.cjs`

## 464ff11 Gate B: keep table helper private for React fast refresh

- `sandpiper.qa.dashboard.client/src/components/DataTable.tsx`

## 4d705d2 Gate C: add baseline-aware execution intelligence and performer productivity

- `Sandpiper.QA.Dashboard.Server/Models/ExecutionIntelligence.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ReportingScope.cs`
- `Sandpiper.QA.Dashboard.Server/Services/ExecutionHistoryService.cs`
- `docs/v2-gates.md`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/components/ActivityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `sandpiper.qa.dashboard.client/src/v2.css`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ExecutionIntelligenceTests.cs`

## 44babcf Gate D: add frozen DSR TCR evidence drafts and Excel PDF exports

- `Sandpiper.QA.Dashboard.Server/Models/ReportSnapshot.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Sandpiper.QA.Dashboard.Server.csproj`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportExporter.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportService.cs`
- `docs/v2-gates.md`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/components/ReportPanel.tsx`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `sandpiper.qa.dashboard.client/src/v2.css`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ExportTests.cs`

## 65d0001 Gate E: add schema-driven defect evidence and explainable quality analysis

- `Sandpiper.QA.Dashboard.Server/Models/ExecutionIntelligence.cs`
- `Sandpiper.QA.Dashboard.Server/Models/QualityEvidence.cs`
- `Sandpiper.QA.Dashboard.Server/Models/ReportSnapshot.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/DefectVerificationEvaluator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/QualityCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsClient.cs`
- `Sandpiper.QA.Dashboard.Server/Services/ExecutionHistoryService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkItemEvidenceService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkspaceEvidenceService.cs`
- `docs/v2-gates.md`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/components/ComparisonPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/QualityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/ReportPanel.tsx`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/DefectVerificationTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/WorkItemEvidenceTests.cs`

## Gate F — enterprise readiness and final integration

- `.gitignore`
- `AGENTS.md`
- `docs/v2-gates.md`
- `docs/v2-handoff.md`
- `package.json`
- `README.md`
- `sandpiper.qa.dashboard.client/e2e/command-center.spec.cjs`
- `sandpiper.qa.dashboard.client/package-lock.json`
- `sandpiper.qa.dashboard.client/package.json`
- `sandpiper.qa.dashboard.client/playwright.config.ts`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/app/ReportingContext.ts`
- `sandpiper.qa.dashboard.client/src/components/ActivityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/DataTable.tsx`
- `sandpiper.qa.dashboard.client/src/components/OperationsPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/QualityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/ReportPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/TableExportContext.ts`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `sandpiper.qa.dashboard.client/src/v2.css`
- `sandpiper.qa.dashboard.client/tests/reporting-context.test.cjs`
- `Sandpiper.QA.Dashboard.Server/Models/Analytics.cs`
- `Sandpiper.QA.Dashboard.Server/Models/QualityEvidence.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/BlockerCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/BugActivityCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ScopeProjector.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/TestPointNormalizer.cs`
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsClient.cs`
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsOptions.cs`
- `Sandpiper.QA.Dashboard.Server/Services/ExecutionHistoryService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/FrontierAnalyticsServices.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Infrastructure/AuditTrail.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Infrastructure/SnapshotCache.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkItemEvidenceService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkspaceEvidenceService.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ApiContractTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/BlockerTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/BugActivityTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/CacheAndScopeTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ExecutionIntelligenceTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/NormalizationTests.cs`
- `VALIDATION.md`
- `docs/v2-changed-files.md`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ReportWorkflowTests.cs`

## Complete KPI/UI enhancement — comparison baseline 43139e8

See kpi-ui-enhancement.md for file purposes, definitions, zero-path evidence and VDI instructions.

- `AGENTS.md`
- `docs/kpi-ui-enhancement.md`
- `docs/reporting-rules.md`
- `README.md`
- `sandpiper.qa.dashboard.client/e2e/command-center.spec.cjs`
- `sandpiper.qa.dashboard.client/src/app/CommandCenter.tsx`
- `sandpiper.qa.dashboard.client/src/app/ReportingContext.ts`
- `sandpiper.qa.dashboard.client/src/components/ActivityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/OverviewKpis.tsx`
- `sandpiper.qa.dashboard.client/src/components/QualityPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/ReportPanel.tsx`
- `sandpiper.qa.dashboard.client/src/components/SearchableCombobox.tsx`
- `sandpiper.qa.dashboard.client/src/services/adoApi.ts`
- `sandpiper.qa.dashboard.client/src/v2.css`
- `sandpiper.qa.dashboard.client/tests/backend-client.test.cjs`
- `sandpiper.qa.dashboard.client/tests/reporting-context.test.cjs`
- `Sandpiper.QA.Dashboard.Server/Models/Analytics.cs`
- `Sandpiper.QA.Dashboard.Server/Models/BusinessMetrics.cs`
- `Sandpiper.QA.Dashboard.Server/Models/ExecutionIntelligence.cs`
- `Sandpiper.QA.Dashboard.Server/Models/QualityEvidence.cs`
- `Sandpiper.QA.Dashboard.Server/Program.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/CaseCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ExecutionCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/HistoryEvidenceEvaluator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/QualityCalculator.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Analytics/ScopeProjector.cs`
- `Sandpiper.QA.Dashboard.Server/Services/AzureDevOpsClient.cs`
- `Sandpiper.QA.Dashboard.Server/Services/ExecutionHistoryService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/Reporting/ReportService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkItemEvidenceService.cs`
- `Sandpiper.QA.Dashboard.Server/Services/WorkspaceEvidenceService.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/AdoIntegrationTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ApiContractTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/BusinessMetricsTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/DefectVerificationTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/ReportWorkflowTests.cs`
- `tests/Sandpiper.QA.Dashboard.Server.Tests/WorkItemEvidenceTests.cs`
- `VALIDATION.md`
- `docs/v2-changed-files.md`
