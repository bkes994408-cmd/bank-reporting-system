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

## MVP-4 第 2 輪：瓶頸分析與優化（2026-03-08）

### 本輪發現的瓶頸

1. **AgentService 註冊重複（Singleton + HttpClient typed client）**
   - 會造成生命週期語意混淆，且不利於明確使用 `HttpClientFactory` 配置。
2. **Request monitoring hot path 計時與路由鍵生成仍可再降成本**
   - 每請求使用 `DateTime.UtcNow` 差值計時。
   - 以 raw path 做 metrics key，對動態路徑會導致高 cardinality（dictionary 成長與聚合成本上升）。
3. **Monitoring route key 組字串有不必要處理**
   - 每請求 `ToUpperInvariant + string interpolation` 會產生多餘處理。

### 本輪優化內容

1. **統一 `IAgentService` 註冊為 typed `HttpClient`**
   - 移除重複 singleton 註冊。
   - 在 `AddHttpClient` 中集中配置：`BaseAddress` 與 `Timeout`。
   - `AgentService` 改為使用相對路徑（`agent-api/...`），避免每次呼叫都做 base URL 拼接。

2. **RequestMonitoringMiddleware hot path 優化**
   - 改用 `Stopwatch.GetTimestamp()` 計時，降低時間計算開銷。
   - 以 endpoint route pattern（`RouteEndpoint.RoutePattern.RawText`）作為路由鍵，降低動態路徑 cardinality。
   - log message 改為 `{Route}`，語意更貼近監控聚合維度。

3. **MonitoringService route key 組裝精簡**
   - 移除 `ToUpperInvariant()`，改為 `string.Concat(method, " ", NormalizePath(path))`。

### 驗證

- 測試：`dotnet test backend.tests/BankReporting.Tests.csproj`
  - 結果：**72 passed / 0 failed**
- 快速基準（本機 warm state，n=30）：
  - `GET /health`：avg **0.260ms**, min 0.194ms, max 0.387ms
  - `GET /metrics`：avg **0.267ms**, min 0.211ms, max 0.384ms

> 註：本機數值僅供趨勢觀察；正式容量規劃仍建議在 staging/prod-like 環境進行壓測。

## 下一步建議

1. 對 `AgentService` 外呼點（declare/reports/news）加入 request timing metric label。
2. 增加簡單負載測試腳本（例如 k6/hey）納入 CI nightly。
3. 若外部 agent 端延遲不穩，導入 timeout/retry policy（Polly）與熔斷保護。
