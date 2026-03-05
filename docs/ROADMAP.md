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
- [ ] 針對現有功能進行性能瓶頸分析和優化
- [ ] 審視程式碼庫，解決潛在的技術債務，提升可維護性
- [ ] 根據使用者回饋或市場新需求，分析並規劃新的功能迭代

## 本輪（Sprint）完成摘要
- 完成 `docs/DEPLOYMENT.md`：Windows Server + Docker Desktop 部署流程（環境準備、Compose 配置、啟動與驗證、故障排查）。
- 完成 `docs/ROLLBACK.md`：部署異常時的標準回滾策略、指令與驗證步驟。
- 完成 `docs/SPRINT-RESULT.md`：記錄本輪 MVP-3 文件交付內容與驗證方式。
- 新增後台帳號管理 API 雛型：`GET /api/admin/accounts`、`PUT /api/admin/accounts/roles`，作為「帳號權限管理」roadmap 項目的後端基礎（已補單元測試）。
