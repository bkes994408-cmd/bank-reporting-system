# Deployment (Windows Server + Docker Desktop)

本文件提供 MVP-0 最小可行部署方式：使用 Docker Desktop（Linux containers）在 Windows Server 上一鍵跑起 **Backend API + Frontend Web**。

## Prerequisites

- Windows Server 2019/2022
- Docker Desktop (Linux containers)
- Git (用來拉 repo)

> 若 Windows Server 環境不允許安裝 Docker Desktop，可改用 Docker Engine + WSL2（依公司政策）。

## Quick start

在 PowerShell：

```powershell
git clone https://github.com/bkes994408-cmd/bank-reporting-system.git
cd bank-reporting-system

docker compose up --build
```

- Frontend: http://localhost:5173/
- Backend (direct): http://localhost:8080/

預設為 **Production** 環境（更接近實際部署）。
如需開啟 Swagger / Development 行為：

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
docker compose up --build
```

- Backend Swagger（Development 環境）：http://localhost:8080/swagger

Frontend 會透過 Nginx 反向代理把 `/api/*` 轉送到 backend，因此前端呼叫 `baseURL: /api` 可正常運作。

## Notes

### HTTPS

後端預設會啟用 HTTPS redirection（依 `ENABLE_HTTPS_REDIRECT` 控制）。
若你在「TLS 終結於反向代理」(proxy terminating TLS) 的情境下用 HTTP 跑容器，可在 compose 或環境變數中設定：

```powershell
$env:ENABLE_HTTPS_REDIRECT = "false"
```

另外，後端已啟用 `ForwardedHeaders`（`X-Forwarded-Proto` 等）以便在反向代理後正確判斷 scheme。

### Ports

預設對外 ports：
- `5173` → Frontend (Nginx)
- `8080` → Backend (ASP.NET Core)

如需修改，請調整 `docker-compose.yml`。

## Troubleshooting

- **Build 很慢**：第一次 build 會下載 NuGet/npm dependencies，屬正常現象。
- **無法連線**：確認 Windows 防火牆允許對外開放 port 5173/8080。
- **容器起不來**：執行 `docker compose logs -f` 查看錯誤訊息。
