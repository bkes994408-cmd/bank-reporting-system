# PERFORMANCE Baseline (MVP-4 第 1 輪)

## 範圍
本輪先做低風險性能盤點與優化，目標是降低 API middleware/服務層的每請求額外開銷。

## 本輪優化

### 1) Request monitoring middleware 降低每請求 DI 查找成本
- 原本在 `Program.cs` 內使用 inline middleware，**每次請求**都透過 `context.RequestServices` 取 logger 與 monitoring service。
- 現在改為 `RequestMonitoringMiddleware`，透過 constructor 注入：
  - `ILogger<RequestMonitoringMiddleware>`
  - `IMonitoringService`
- 效果：減少 hot path 的 service lookup 與 logger 建立成本，邏輯維持不變。

### 2) 移除 `AgentService` 無效初始化
- 移除未使用的 `IConfiguration` 欄位。
- 移除建構子中未被使用的 `HttpClientHandler` 建立（避免無意義配置與誤導）。

## 測試方式（本機）

啟動 API：

```bash
dotnet run --project backend/BankReporting.Api.csproj
```

基準測試（50 次請求，curl `time_total`）：

- `GET /health`
- `GET /api/admin/users`（header: `X-Role: admin`）

## 測試結果（本機，warm state）

- `/health`: avg **0.0004s**, min 0.0003s, max 0.0012s (n=50)
- `/api/admin/users`: avg **0.0004s**, min 0.0003s, max 0.0006s (n=50)

> 註：這是本機開發環境（低延遲 loopback）結果，絕對數值僅供趨勢參考。上線前應在 staging/production-like 環境做壓測（含外部 agent API 延遲）。

## 下一步建議

1. 對 `AgentService` 外呼點（declare/reports/news）加入 request timing metric label。
2. 增加簡單負載測試腳本（例如 k6/hey）納入 CI nightly。
3. 若外部 agent 端延遲不穩，導入 timeout/retry policy（Polly）與熔斷保護。
