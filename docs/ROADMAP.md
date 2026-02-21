# Roadmap / MVP Checklist (bank-reporting-system)

> 規則：所有功能合併需走 PR，並附完整測試證據（見 REVIEWING.md / PR template）。

## MVP-0：專案可開發/可測/可部署（門檻）
- [ ] CI 全綠（後端測試 + 前端 build）
- [ ] 前端依賴鎖定（提交 package-lock.json 或改用 pnpm-lock）
- [ ] Docker（dev/prod）可一鍵起服務（API + Web）
- [ ] Healthcheck endpoint（/health 或等效）
- [ ] 移除 repo 內建置產物（bin/obj/.vs）並維持 .gitignore

## MVP-1：核心業務流程（功能）
- [x] Excel 解析 → JSON（/api/parsing/*）有明確輸入/輸出契約 + 錯誤碼
- [ ] 申報上傳（/api/declare）含簽章/JWE 流程（若適用）
- [ ] 查詢上傳結果（/api/declare/result）
- [ ] 當月應申報報表（/api/reports）
- [ ] 申報歷程（/api/reports/histories）
- [x] 金鑰匯入/驗證（/api/keys/*）
- [ ] Token 更新（/api/token/update）

## MVP-2：完整功能測試（必備）
- [ ] 後端 unit tests 覆蓋核心 service（>= 70% 行為覆蓋）
- [ ] 後端 integration tests 覆蓋 3 條主流程（解析→申報→查詢）
- [ ] 前端 e2e（Playwright/Cypress 擇一）覆蓋 1 條主流程
- [x] 重要安全測試：無 secrets、日誌不含敏感資料、輸入驗證

## MVP-3：PRD 部署與回滾
- [ ] Windows Server（Docker Desktop）部署指南（docs/DEPLOYMENT.md）
- [ ] 回滾指南（docs/ROLLBACK.md）
- [ ] 監控/告警最小集合（logs + basic metrics）
