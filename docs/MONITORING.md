# Monitoring / Alerting (MVP 最小集合)

本文件說明 MVP 階段的最小監控與告警能力（logs + basic metrics）。

## 1) 日誌（Logs）

系統會在每次 API 請求完成後輸出：
- HTTP Method
- Path
- StatusCode
- Duration(ms)

### 告警級別 log（Warning）

符合以下任一條件會額外輸出 `ALERT` warning：
1. HTTP 狀態碼 >= 500
2. 請求耗時 >= 2000ms

## 2) 指標（Metrics）

端點：`GET /metrics`

格式：Prometheus text format（`text/plain; version=0.0.4`）

### 目前提供指標
- `bank_reporting_requests_total`
- `bank_reporting_errors_total`
- `bank_reporting_request_duration_ms_avg`
- `bank_reporting_route_requests_total{route="..."}`
- `bank_reporting_route_errors_total{route="..."}`

## 3) 驗證方式

### 本地快速驗證
```bash
# 啟動 API
cd backend
dotnet run

# 產生一些請求後抓 metrics
curl -s http://localhost:5000/metrics
```

### 測試
- `backend.tests/ControllersTests.cs` 已補充 `MonitoringControllerTests`：
  - 驗證 `/metrics` 回傳格式
  - 驗證 5xx 會累計在 error metrics
