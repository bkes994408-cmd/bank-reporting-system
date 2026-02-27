# Sprint Result (2026-02-27)

## 目標達成狀態

1. **CI 修復（主要 pipeline）** ✅
   - 調整 `.github/workflows/ci.yml`：
     - .NET 版本改為 `10.0.x`
     - Restore/Test 改跑 `BankReporting.sln`

2. **前端依賴鎖定** ✅
   - 已提交 `frontend/package-lock.json`（既有檔案，並使用 `npm ci` 驗證可重現安裝）

3. **Docker dev/prod 一鍵啟動** ✅
   - 新增：
     - `backend/Dockerfile`
     - `frontend/Dockerfile`
     - `frontend/nginx.conf`
     - `docker-compose.dev.yml`
     - `docker-compose.yml`
     - `.dockerignore`
   - `vite.config.js` 支援 `VITE_API_PROXY_TARGET`，供容器化開發代理。
   - `Program.cs` 增加 `DISABLE_HTTPS_REDIRECTION` 支援，避免容器內 HTTP redirect 問題。

4. **核心 API 補齊（至少 1-2 項）** ✅
   - 補強 `/api/reports`：
     - `bankCode`、`applyYear` 必填
     - `applyMonth` 需為 `01~12`
     - 請求欄位 trim 後再送 service
   - 補強 `/api/reports/histories`：
     - `bankCode`、`reportId`、`year` 必填
     - 請求欄位 trim 後再送 service
   - 對應單元測試補齊（`backend.tests/ControllersTests.cs`）

## 測試與驗證證據

### 後端測試
```bash
dotnet test -c Release
```
結果：**40 passed, 0 failed**

### 前端安裝與建置
```bash
cd frontend
npm ci
npm run build
```
結果：**build 成功**（Vite 輸出 dist 檔）

### Docker（最小驗證步驟）

#### Dev
```bash
docker compose -f docker-compose.dev.yml up --build
# 開啟 http://localhost:5173
# API health: http://localhost:5000/health
```

#### Prod
```bash
docker compose up --build
# 開啟 http://localhost:5173
# API health: http://localhost:5000/health
```

## 變更檔案
- `.github/workflows/ci.yml`
- `backend/Controllers/ReportsController.cs`
- `backend/Program.cs`
- `backend.tests/ControllersTests.cs`
- `frontend/vite.config.js`
- `backend/Dockerfile` (new)
- `frontend/Dockerfile` (new)
- `frontend/nginx.conf` (new)
- `docker-compose.dev.yml` (new)
- `docker-compose.yml` (new)
- `.dockerignore` (new)
- `docs/ROADMAP.md`
- `docs/SPRINT-RESULT.md` (new)

## 阻塞與建議拆分
- **目前無阻塞。**
- 建議下一輪拆分：
  1. 加入 Docker smoke test 到 CI（`docker compose config` + image build）
  2. 補前端 e2e 到 CI
  3. 清理 repo 內 `bin/obj` 產物並強化 `.gitignore`
