# Windows Server（Docker Desktop）部署指南

> 適用範圍：在 Windows Server（Desktop Experience）以 Docker Desktop + Docker Compose 部署本專案 API（backend）與 Web（frontend）。

---

## 1. 環境準備

### 1.1 作業系統與權限
- Windows Server 2019/2022（建議已套用最新安全更新）
- 本機系統管理員權限（安裝軟體、啟用 Windows Feature）

### 1.2 必要工具
- Docker Desktop for Windows
- Git for Windows
- PowerShell 5.1+（建議 PowerShell 7）

### 1.3 網路與防火牆
請確認可連線到：
- GitHub（原始碼）
- NuGet / npm Registry（映像建置時下載套件）

建議開放連入埠：
- `8080`：前端網站（Nginx）
- `5000`：後端 API

> 若 API 只供前端內網存取，可僅開放 8080，5000 僅限內部網段。

### 1.4 Docker Desktop 驗證
安裝後先確認 Docker Engine 可用：

```powershell
docker version
docker info
docker compose version
```

若可正常顯示 Client/Server 與 Compose 版本，代表環境可部署。

---

## 2. 取得程式碼與版本

```powershell
git clone https://github.com/bkes994408-cmd/bank-reporting-system.git
cd bank-reporting-system
```

建議固定部署版本（tag 或 commit），避免直接部署未驗證 HEAD：

```powershell
git fetch --all --tags
git checkout <release-tag-or-commit>
```

---

## 3. Docker Compose 配置說明

專案使用 `docker-compose.yml`，包含三個服務：

- `backend`
  - Dockerfile：`backend/Dockerfile`
  - 容器埠：5000
  - 主機對映：`5000:5000`
- `frontend`
  - Dockerfile：`frontend/Dockerfile`
  - 主機對映：`8080:80`
- `frontend-dev`（僅 dev profile）
  - Dockerfile：`frontend/Dockerfile.dev`
  - 主機對映：`5173:5173`

若要客製埠號，可修改 `docker-compose.yml` 的 `ports` 後再啟動。

---

## 4. 啟動指令

### 4.1 Production 啟動（建議正式環境）

```powershell
docker compose up -d --build
```

### 4.2 Development 啟動（含 Vite dev server）

```powershell
docker compose --profile dev up -d --build
```

### 4.3 服務狀態與日誌

```powershell
docker compose ps
docker compose logs --tail=200 backend
docker compose logs --tail=200 frontend
docker compose logs --tail=200 frontend-dev
```

---

## 5. 部署驗證

### 5.1 基本連線
- 前端（Prod）：`http://<server-ip>:8080/`
- 後端健康檢查：`http://<server-ip>:5000/health`
- 後端 metrics：`http://<server-ip>:5000/metrics`

### 5.2 驗證清單（建議）
1. `docker compose ps` 服務狀態皆為 `Up`
2. `/health` 回傳成功
3. 前端可正常載入
4. 至少 1 條核心 API（如 `/api/reports`）可正常回應

---

## 6. 版本更新部署（滾動到新版本）

```powershell
cd <repo-path>
git fetch --all --tags
git checkout <new-tag-or-commit>
docker compose up -d --build
```

更新完成後重做「第 5 節部署驗證」。

---

## 7. 常見問題排查

### 7.1 Docker Desktop 無法啟動
- 先重啟 Docker Desktop 與主機
- 確認 Docker Engine 狀態為 Running
- 以 `docker info` 檢查是否可連線 daemon

### 7.2 `docker compose up` 卡住或建置失敗
- 檢查網路是否可連外（NuGet / npm registry）
- 重新建置：
  ```powershell
  docker compose build --no-cache
  docker compose up -d
  ```
- 查看失敗服務日誌：
  ```powershell
  docker compose logs --tail=200 <service>
  ```

### 7.3 前端可開但 API 連不到
- 確認 `backend` 服務狀態是否 `Up`
- 確認防火牆允許 `5000`
- 用 `curl http://localhost:5000/health` 在主機本機先測

### 7.4 容器反覆重啟
- 查看重啟計數：`docker compose ps`
- 查看錯誤日誌：`docker compose logs --tail=200 <service>`
- 若是新版本問題，依 `docs/ROLLBACK.md` 執行回滾

---

## 8. 維運建議（最小可行）

- 部署前記錄版本（tag/commit）
- 保留最近部署與回滾操作紀錄
- 定期檢查 `/health`、`/metrics` 與容器日誌
- 發版與事故處理流程請搭配 `docs/ROLLBACK.md`
