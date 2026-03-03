# Sprint Result (2026-02-28)

## 本輪成果總結（MVP-2 + MVP-3）

1. **前端 e2e 主流程測試** ✅
   - 以 Playwright 實作 `frontend/e2e/main-flow.spec.js`
   - 覆蓋 smoke 主流程（公告頁 → 申報上傳頁 → 必填提示驗證）
   - 針對 `GET /api/news` 做 route mock，避免外部依賴不穩定

2. **CI 納入 e2e 並維持全綠** ✅
   - 更新 `.github/workflows/ci.yml`：
     - `backend`：`dotnet test`
     - `frontend-build`：`npm ci` + `npm run build`
     - `frontend-e2e`：`npm ci` + `npx playwright install --with-deps chromium` + `npm run test:e2e`

3. **MVP-3 部署與回滾文件完成** ✅
   - `docs/DEPLOYMENT.md`：Windows Server + Docker Desktop 部署、驗證、排查流程
   - `docs/ROLLBACK.md`：回滾策略、標準步驟、失敗處置與紀錄模板

4. **Roadmap 文件同步更新** ✅
   - `docs/ROADMAP.md` 已同步反映 MVP-2/MVP-3 交付狀態

## 本地驗證結果（歷次）

### 後端測試
```bash
dotnet test BankReporting.sln -c Release
```
結果：**56 passed, 0 failed**

### 前端 build
```bash
cd frontend
npm run build
```
結果：**build success**

### 前端 e2e（Playwright）
```bash
cd frontend
npm run test:e2e
```
結果：**1 passed, 0 failed**

## 變更檔案
- `.github/workflows/ci.yml`
- `docs/DEPLOYMENT.md`
- `docs/ROLLBACK.md`
- `docs/ROADMAP.md`
- `docs/SPRINT-RESULT.md`
