# Roadmap / MVP Checklist (bank-reporting-system)

> 規則：所有功能合併需走 PR，並附完整測試證據（見 REVIEWING.md / PR template）。

## MVP-0：專案可開發/可測/可部署（門檻）
- [ ] CI 全綠（後端測試 + 前端 build）
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

## MVP-2：完整功能測試（必備）
- [x] 後端 unit tests 覆蓋核心 service（>= 70% 行為覆蓋）
- [x] 後端 integration tests 覆蓋主流程（解析→申報→查詢 + reports 路徑）
- [x] 前端 e2e（Playwright/Cypress 擇一）覆蓋 1 條主流程
- [x] 重要安全測試：無 secrets、日誌不含敏感資料、輸入驗證

## MVP-3：PRD 部署與回滾
- [x] Windows Server（Docker Desktop）部署指南（docs/DEPLOYMENT.md）
- [x] 回滾指南（docs/ROLLBACK.md）
- [x] 監控/告警最小集合（logs + basic metrics）

## 本輪（Sprint）完成摘要
- 補上 AgentService 核心分支單元測試（含 multipart 解析、fallback、例外路徑）。
- 新增 reports/histories integration flow，驗證 API 輸入清洗後可正常查詢。
- 清理 `.DS_Store` 追蹤並補齊 `.gitignore`（TestResults/coverage/frontend dist）。
- 修正 CI .NET 版本與 restore/test 範圍，避免 `backend.tests --no-restore` 失敗。
- 補強 `/api/reports` 與 `/api/reports/histories` 輸入驗證（必填與月份格式）。
- 提供 `docker-compose.dev.yml` + `docker-compose.yml` 一鍵啟動（dev/prod）。
