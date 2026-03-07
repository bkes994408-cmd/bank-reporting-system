# MVP-4 Next Iteration Plan

## Goal
在現有 MVP 功能穩定後，規劃下一輪可落地迭代，優先提升可維運性、可觀測性與使用者操作效率。

## Priority Backlog

### P0 (本輪先做)
1. **CI/Quality Gate 強化**
   - 增加 backend test coverage threshold 檢查
   - 增加 frontend lint/type-check job
   - 驗收：PR 必須通過 backend + frontend + quality gate

2. **權限治理收斂（RBAC hardening）**
   - admin API 改成可配置策略（非僅 header）
   - 新增 role matrix 文件與測試
   - 驗收：非 admin 無法存取 admin route，測試覆蓋 100%

3. **性能觀測擴充**
   - 對 agent 外呼加入 latency metrics（endpoint label）
   - 新增慢請求統計與 top N 路徑
   - 驗收：`/api/metrics` 可看到外呼耗時統計

### P1 (下一輪)
4. **後台管理可用性提升**
   - 角色編輯 UI、操作回饋與錯誤提示
   - 驗收：可在 UI 完成新增使用者與角色調整

5. **設定管理一致化**
   - appsettings schema 文件化與驗證
   - 驗收：啟動時對必要設定做檢核

### P2 (觀察)
6. **宣告流程重試與熔斷策略**
   - 使用 Polly 對外部 API 做 retry/backoff
   - 驗收：外部暫時失敗時成功率提升且不造成雪崩

## Execution Strategy
- 每個 PR 控制在單一主題（docs / backend / frontend）
- 每個 PR 需附：測試證據、風險、回滾方式
- 每個主題先交最小可驗收版本，再迭代

## Risk
- 目前仍有 local `backend/Dockerfile` 變更噪音，需避免混入功能 PR

## Definition of Done
- 有 roadmap 對應勾選
- 有測試與 build 證據
- 可在 1 個 PR 內被 reviewer 快速理解與驗收
