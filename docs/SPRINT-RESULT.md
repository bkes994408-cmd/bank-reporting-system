# Sprint Result (2026-02-27)

## 本次加強（MVP-2 測試與穩定性）

1. **核心 service 單元測試補強** ✅
   - 擴充 `backend.tests/ServiceTests.cs`：
     - `AgentService` 高價值路徑：multipart request 組裝、fallback（null body）、exception handling、附件下載成功/失敗。
     - 覆蓋方法包含：`ParseExcelAsync`、`ParseExcelWithContactAsync`、`DeclareAsync`、`GetDeclareResultAsync`、`GetMonthlyReportsAsync`、`GetReportHistoriesAsync`、`ImportKeysAsync`、`UpdateTokenAsync`、`CheckVersionAsync`、`GetAgentInfoAsync`、`GetNewsAsync`、`ValidateKeysAsync`、`DownloadAttachmentAsync`。

2. **Integration flow 新增** ✅
   - 擴充 `backend.tests/Integration/HappyPathIntegrationTests.cs`：
     - 新增 `HappyPath_Reports_And_Histories_ReturnsOk`
     - 覆蓋 `/api/reports` 與 `/api/reports/histories`，並驗證 trim 後請求仍可正常走通。

3. **建置產物與忽略規則清理** ✅
   - `.gitignore` 補齊：`TestResults/`、coverage 檔、`frontend/dist/`、`.idea/`、`.vscode/`
   - 移除 repo 追蹤的 `.DS_Store`

## 測試與覆蓋率證據

### 後端測試
```bash
dotnet test -c Release
```
結果：**56 passed, 0 failed**

### 服務層行為覆蓋（XPlat Code Coverage）
```bash
dotnet test backend.tests/BankReporting.Tests.csproj -c Release --collect:"XPlat Code Coverage"
```
結果（依 coverage.cobertura.xml 統計）：
- `Services/AgentService.cs`: **76.4%** (81/106)
- `Services/ExcelParsingService.cs`: **76.9%** (113/147)
- `Services/MonitoringService.cs`: **95.0%** (38/40)

> 註：整體專案 line-rate 約 61.88%，但 MVP-2 本輪目標聚焦「核心 service 行為覆蓋 >=70%」，已達成。

## 變更檔案
- `backend.tests/ServiceTests.cs`
- `backend.tests/Integration/HappyPathIntegrationTests.cs`
- `.gitignore`
- `docs/ROADMAP.md`
- `docs/SPRINT-RESULT.md`
- `.DS_Store`（從 git 追蹤移除）

## 後續建議
- 補前端 e2e（Playwright）至少 1 條主流程，讓 MVP-2 全面達標。
- 後端 coverage 可再補 controller/Program middleware 測試，提升整體覆蓋率。
