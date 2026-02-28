# Sprint Result (2026-02-28)

## 本次加強（MVP-2 前端 e2e + CI 全綠）

1. **前端 e2e 主流程測試** ✅
   - 以 Playwright 實作 `frontend/e2e/main-flow.spec.js`
   - 覆蓋 smoke 主流程：
     - 進入公告資訊首頁
     - 切換到「申報上傳」頁
     - 觸發「確認上傳」並驗證必填提示訊息
   - 針對 `GET /api/news` 做 route mock，避免外部 API 依賴造成不穩定。

2. **CI 納入 e2e** ✅
   - 更新 `.github/workflows/ci.yml`：
     - `backend`：`dotnet test`
     - `frontend-build`：`npm ci` + `npm run build`
     - `frontend-e2e`：`npm ci` + `npx playwright install --with-deps chromium` + `npm run test:e2e`
   - 讓前端 e2e 可在 GitHub Actions 重現執行。

3. **Roadmap 狀態更新** ✅
   - `docs/ROADMAP.md` 勾選：
     - 「前端 e2e（Playwright）覆蓋 1 條主流程」
     - 「CI 全綠（後端測試 + 前端 build + 前端 e2e）」

## 本地驗證結果

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
- `docs/ROADMAP.md`
- `docs/SPRINT-RESULT.md`

## 備註
- 本輪採最小必要改動，未調整既有前後端架構，只補齊 e2e 與 CI 覆蓋。
