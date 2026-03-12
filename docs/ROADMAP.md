# Roadmap / MVP Checklist (bank-reporting-system)

> 規則：所有功能合併需走 PR，並附完整測試證據（見 REVIEWING.md / PR template）。

## MVP-0：專案可開發/可測/可部署（門檻）
- [x] CI 全綠（後端測試 + 前端 build）
- [x] 前端依賴鎖定（已提交 frontend/package-lock.json，CI 使用 npm ci）
- [x] Docker（dev/prod）可一鍵起服務（API + Web）
- [x] Healthcheck endpoint（/health 或等效）
- [x] 移除 repo 內建置產物（bin/obj/.vs）並維持 .gitignore

## MVP-1：核心業務流程（功能）
- [x] Excel 解析 → JSON（/api/parsing/*）有明確輸入/輸出契約 + 錯誤碼
- [x] 申報上傳（/api/declare）含簽章/JWE 流程（若適用）
- [x] 查詢上傳結果（/api/declare/result）
- [x] 當月應申報報表（/api/reports）
- [x] 申報歷程（/api/reports/histories）
- [x] 金鑰匯入/驗證（/api/keys/*）
- [x] Token 更新（/api/token/update）
- [x] AD網域登入
- [x] 後臺管理畫面
- [x] 帳號權限管理

## MVP-2：完整功能測試（必備）
- [x] 後端 unit tests 覆蓋核心 service（>= 70% 行為覆蓋）
- [x] 後端 integration tests 覆蓋主流程（解析→申報→查詢 + reports 路徑）
- [x] 前端 e2e（Playwright/Cypress 擇一）覆蓋 1 條主流程
- [x] 重要安全測試：無 secrets、日誌不含敏感資料、輸入驗證

## MVP-3：PRD 部署與回滾
- [x] Windows Server（Docker Desktop）部署指南（docs/DEPLOYMENT.md）
- [x] 回滾指南（docs/ROLLBACK.md）
- [x] 監控/告警最小集合（logs + basic metrics）

## MVP-4：持續優化與新功能
- [x] 針對現有功能進行性能瓶頸分析和優化
- [x] 審視程式碼庫，解決潛在的技術債務，提升可維護性
- [x] 根據使用者回饋或市場新需求，分析並規劃新的功能迭代

## MVP-5：進階整合與合規
- [x] 第三方系統 API 整合（如會計軟體、ERP）
- [x] 歷史數據歸檔與查詢優化
- [x] 合規性審計報告自動化（含稽核軌跡 Audit Trail 與操作留痕查詢）
- [x] 匯出報表與申報結果的加密封存策略
- [x] 對外整合重試/補償機制（含死信佇列）

## MVP-6：智能合規與生態整合

*   [x] 自動化法規更新監測與影響分析
    *   核心功能：開發自動化工具，監測各大監管機構（如金管會、中央銀行）發布的最新法規文件、指引變更。
    *   智能解析：應用自然語言處理 (NLP) 和文本挖掘技術，智能識別法規條文中的關鍵變動、新增要求、影響範圍。
    *   影響評估：自動分析法規變動對現有申報業務流程、數據採集規範、報告格式的潛在影響，並產生影響分析報告。
    *   難點：法規條文的複雜性與多樣性，將非結構化文本轉化為可操作的業務規則。

*   [x] 與外部合規平台/數據源集成
    *   監管機構接口對接：開發標準化的 API 接口或數據傳輸模組，實現與監管機構、第三方合規平台（如 KYC/AML 服務提供商）的數據自動交換。
    *   外部風險數據導入：支持導入外部制裁名單、PEP (政治公眾人物) 名單、不良資產清單等，用於自動化風險比對和審核。
    *   數據標準化與轉換：開發彈性的數據轉換引擎，將內部數據格式自動匹配外部系統的要求，並處理數據驗證與清洗。
    *   難點：異構系統的數據對接複雜性，數據安全與隱私保護的合規性。

*   [x] 預警與異常行為檢測
    *   機器學習驅動的異常檢測模型：應用監督式或非監督式機器學習模型，分析歷史申報數據、交易記錄、用戶行為，識別潛在的洗錢、詐欺或其他不合規行為模式。
    *   可配置的預警規則與閥值：提供靈活的配置界面，允許合規人員自定義預警規則、設定風險閾值，並支持多級別（如：低、中、高）的告警通知。
    *   告警觸發與響應機制：當檢測到異常行為時，自動觸發告警通知（郵件、簡訊、應用內通知），並引導合規人員進行調查，生成詳細的異常報告。
    *   難點：高精度異常檢測模型的訓練與調優，降低誤報率，確保模型可解釋性。

*   [x] 區塊鏈技術應用探索 (可選)
    *   關鍵審計軌跡上鏈：探索將關鍵的審計軌跡、申報證明、法規符合性聲明等核心數據以加密形式上傳至區塊鏈，利用其不可篡改性與時間戳特性，增強數據的公信力與可追溯性。
    *   基於區塊鏈的數據共享方案：研究如何在保護隱私的前提下，利用區塊鏈技術實現銀行間或監管機構與銀行之間的安全、高效數據共享。
    *   難點：區塊鏈技術在金融領域的合規性與監管接受度，性能與擴展性問題。

*   [x] 用戶行為分析與稽核追溯優化
    *   增強用戶操作日誌詳盡度：細化記錄系統中所有用戶的操作行為，包括登錄、數據查詢、數據修改、報告生成等，提供更豐富的稽核數據。
    *   可視化稽核追溯路徑：開發直觀的圖形化界面，允許合規人員快速追溯任何數據變動或報告生成的完整鏈路，快速定位問題源頭和責任人。
    *   行為分析與審計效率優化：引入行為分析工具，識別合規流程中的瓶頸或重複性操作，提供優化建議，提升稽核效率與準確性。
    *   難點：海量日誌數據的高效儲存與查詢，數據隱私與合規性之間的平衡。

## 本輪（Sprint）完成摘要
- 完成 `docs/DEPLOYMENT.md`：Windows Server + Docker Desktop 部署流程（環境準備、Compose 配置、啟動與驗證、故障排查）。
- 完成 `docs/ROLLBACK.md`：部署異常時的標準回滾策略、指令與驗證步驟。
- 完成 `docs/SPRINT-RESULT.md`：記錄本輪 MVP-3 文件交付內容與驗證方式。
- 完成 AD 登入 + 後台管理 + 帳號權限管理（PR #51）。
- 完成 MVP-5 首項「第三方系統 API 整合（如會計軟體、ERP）」：新增 `/api/integrations/third-party/systems`、`/api/integrations/third-party/sync` 與可配置整合設定 `ThirdPartyIntegrations:Systems`。
- 完成 MVP-5 項目「歷史數據歸檔與查詢優化」：新增 `/api/reports/histories/archive` 與 `/api/reports/histories/archive/query`，支援歸檔封存、條件查詢與分頁。
- 完成 MVP-5 項目「合規性審計報告自動化」：新增 `/api/compliance/audit-reports/generate`、`/api/compliance/audit-reports/query`、`/api/compliance/audit-trails/query`，並在 request middleware 自動留存稽核軌跡。
- 完成 MVP-5 項目「匯出報表與申報結果的加密封存策略」：新增 `/api/reports/secure-archive/report-histories`、`/api/reports/secure-archive/declare-result`、`/api/reports/secure-archive/query`，採用 AES-GCM 封存並以遮罩 metadata 查詢。
- 完成 MVP-5 項目「對外整合重試/補償機制（含死信佇列）」：第三方同步導入 retry/backoff、補償呼叫（compensation path）、死信佇列查詢與人工重送 API（`/api/integrations/third-party/dead-letters`、`/api/integrations/third-party/dead-letters/{deadLetterId}/retry`）。
- 完成 MVP-6 項目「自動化法規更新監測與影響分析」：新增法規快照寫入、版本差異比對、規則式影響評估與建議動作，並提供 API（`/api/compliance/regulations/snapshots`、`/api/compliance/regulations/impact-analysis/generate`、`/api/compliance/regulations/impact-analysis/query`）。
- 完成 MVP-6 項目「與外部合規平台/數據源集成」：新增外部合規數據源同步與風險名單比對能力，支援欄位映射標準化與風險決策建議 API（`/api/compliance/external-data/sync`、`/api/compliance/external-data/screen`）。
- 完成 MVP-6 項目「預警與異常行為檢測」：新增可配置告警規則、告警評估與查詢能力，提供 API（`/api/compliance/alerts/rules/upsert`、`/api/compliance/alerts/rules/query`、`/api/compliance/alerts/evaluate`、`/api/compliance/alerts/query`），支援失敗請求突增/高風險操作/夜間敏感操作異常檢測與建議處置。
- 完成 MVP-6 可選項「區塊鏈技術應用探索」：新增稽核錨點寫入/查詢與跨機構共享方案模擬 API（`/api/compliance/blockchain/anchors/commit`、`/api/compliance/blockchain/anchors/query`、`/api/compliance/blockchain/sharing/simulate`），支援 hash-chain 溯源與共享風險策略建議。
- 完成 MVP-6 項目「用戶行為分析與稽核追溯優化」：擴充稽核軌跡查詢條件（敏感操作、狀態碼、耗時），新增行為洞察 API（`/api/compliance/audit-trails/behavior-insights`）與追溯路徑 API（`/api/compliance/audit-trails/trace`），提供 Top users/paths 與優化建議。
- 完成效能基準與 request-path 優化（`docs/PERFORMANCE.md` + middleware 重構，PR #52）。
- 完成技術債清理：移除未使用 legacy `AccountAdminService`（PR #53）。
- 完成 MVP-4 規劃與 RBAC hardening：新增 `docs/NEXT-ITERATION-PLAN.md`、`docs/RBAC-MATRIX.md`、operator route guard（PR #54, #55）。
- 完成「根據使用者回饋或市場新需求，分析並規劃新的功能迭代」：新增 `docs/FEATURE-ITERATION-ANALYSIS.md`（本 PR）。
