# Windows Server（Docker Desktop）部署指南

> 適用目標：以 Windows Server（含 Desktop Experience）+ Docker Desktop 部署本專案（backend + frontend）。
>
> 本文件對應 `docs/ROADMAP.md` 的 MVP-3 項目：`Windows Server（Docker Desktop）部署指南（docs/DEPLOYMENT.md）`。

---

## 1. 部署前提

- 作業系統：Windows Server 2019/2022（建議已更新至最新修補）
- 權限：可安裝軟體與啟用 Windows Feature 的系統管理員帳號
- 必要軟體：
  - [Docker Desktop for Windows](https://www.docker.com/products/docker-desktop/)
  - Git（用於拉取程式碼）
- 網路：可連線 GitHub / 套件來源（NuGet、npm）
- 開放埠（可依實際需求調整）：
  - `80`（前端）
  - `5000`（後端 API，若僅內部供前端存取可不對外）

> 注意：Docker Desktop 在 Windows Server 的支援條件與授權政策，請以 Docker 官方文件與貴組織內規為準。

---

## 2. 安裝與驗證 Docker Desktop

1. 安裝 Docker Desktop。
2. 啟動 Docker Desktop，確認引擎狀態為 Running。
3. 開啟 PowerShell 驗證：

```powershell
docker version
docker info
```

若可正常輸出 Client/Server 資訊，代表安裝成功。

---

## 3. 取得程式碼

```powershell
git clone https://github.com/bkes994408-cmd/bank-reporting-system.git
cd bank-reporting-system
```

建議切到要部署的版本（tag 或指定 commit），避免直接部署未驗證的最新程式碼。

---

## 4. 容器化檔案（已內建）

專案已內建下列檔案，可直接一鍵啟動：

- `backend/Dockerfile`
- `frontend/Dockerfile`（production）
- `frontend/Dockerfile.dev`（development）
- `frontend/nginx.conf`
- `docker-compose.yml`

---

## 5. 建置與啟動

於專案根目錄執行：

### 5.1 Production（預設）

```powershell
docker compose up -d --build
```

### 5.2 Development（含前端 Vite dev server）

```powershell
docker compose --profile dev up -d --build
```

檢查狀態：

```powershell
docker compose ps
docker compose logs -f backend
docker compose logs -f frontend
docker compose logs -f frontend-dev
```

---

## 6. 驗證部署

- 前端首頁（production）：`http://<server-ip>:8080/`
- 前端首頁（development）：`http://<server-ip>:5173/`
- 後端健康狀態：`http://<server-ip>:5000/health`
- 監控指標：`http://<server-ip>:5000/metrics`

建議驗證：
1. 前端頁面可正常載入。
2. 後端 API 可回應（例如 `/api/info`）。
3. `docker compose ps` 顯示服務皆為 `Up`。

---

## 7. 更新部署（新版本）

```powershell
git fetch --all
git checkout <target-tag-or-commit>
docker compose build --no-cache
docker compose up -d
```

完成後重做第 6 節驗證。

---

## 8. 常見問題

### 8.1 Docker Desktop 無法啟動
- 先確認 Windows 功能（虛擬化/容器）是否啟用。
- 檢查 BIOS/Hyper-V/WSL 相關設定是否符合 Docker Desktop 要求。
- 參考 Docker Desktop 官方疑難排解文件。

### 8.2 前端可開啟但 API 呼叫失敗
- 檢查 `backend` 容器是否正常 (`docker compose logs backend`)。
- 確認防火牆是否允許 `5000` 埠。
- 若前端需反向代理 API，請補上 Nginx 設定。

### 8.3 容器持續重啟
- 用 `docker compose logs <service>` 檢查錯誤。
- 確認映像是否成功建置、環境變數是否完整。

---

## 9. 安全與維運建議（最小）

- 不要把敏感憑證寫死在映像中；請改用環境變數或機密管理工具。
- 限制對外開放埠，只暴露必要服務。
- 至少保留 7~14 天容器日誌，並定期檢查 `5xx` 與高延遲請求。
- 正式環境建議搭配 `docs/ROLLBACK.md` 進行版本回滾流程。