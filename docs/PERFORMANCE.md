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

## MVP-5/6 穩定性提升：稽核查詢與一致性基準（2026-03-20）

### 測試目標
驗證 `ComplianceAuditService.QueryTrace` 在高讀取 + 持續寫入情境下，是否仍維持：

- 查詢結果排序正確（依 `TimestampUtc`）
- `MaxSteps` 上限不被突破
- 無例外、無死鎖、無資料競態造成的不一致

### 實作內容

1. **新增 / 擴充 BenchmarkDotNet 基準專案**
   - 路徑：`backend.benchmarks/`
   - 基準項目：
     - `QueryTrace_ByTraceId`
     - `QueryTrails_Filtered`
     - `CheckDataIntegrity_StandardWindow`

2. **審計查詢效能優化（Service hot path）**
   - `QueryTrails` 針對「無篩選條件」場景走快速分頁路徑，避免全量比對。
   - 篩選查詢預先正規化條件（trim / null coalesce）以降低逐筆比對開銷。

3. **Data Integrity Check 強化**
   - 新增 `riskLevel` 合法性檢查（low/medium/high）。
   - 新增同一 `traceId` 的 user 一致性與 method/path 一致性檢查。
   - 新增報告摘要與時間欄位一致性檢查（`uniqueUsers <= totalRequests`、`generatedAtUtc >= endDateUtc`）。

4. **API 錯誤處理強化**
   - `ApiExceptionHandlingMiddleware` 新增 timeout 類錯誤映射（`API_5040` / HTTP 504）。
   - `Program.cs` 新增 `InvalidModelStateResponseFactory`，統一模型驗證錯誤回應格式。

5. **新增壓力測試（xUnit）**
   - 檔案：`backend.tests/ComplianceAuditPerformanceTests.cs`
   - 測試項目：
     - `QueryTrace_Benchmark_BaselineUnderLoad`
       - 10k seed records，重複 1000 次查詢
       - 檢查 avg / p95 latency 閥值與資料正確性
     - `QueryTrace_StressTest_RemainsStableUnderConcurrentReadWrite`
       - 12 reader + 4 writer 併發持續 8 秒
       - 驗證排序、step 上限與穩定性（無錯誤）

### 執行指令

```bash
# 單元 + 壓力測試
dotnet test BankReporting.sln -c Release

# 基準測試（BenchmarkDotNet）
dotnet run -c Release --project backend.benchmarks/BankReporting.Benchmarks.csproj -- --filter "*ComplianceAuditBenchmarks*"
```

### 本機結果（Apple Silicon / .NET 10.0.3）

- `dotnet test`：**163 passed / 0 failed**（含壓力測試與一致性/錯誤處理測試）
- Benchmark：
  - `QueryTrace_ByTraceId`：**15.87 us**
  - `QueryTrails_Filtered`：**82.77 us**
  - `CheckDataIntegrity_StandardWindow`：**3.67 ms**

> Benchmark 報表輸出：`BenchmarkDotNet.Artifacts/results/BankReporting.Benchmarks.ComplianceAuditBenchmarks-report-github.md`

## 下一步建議

1. 對 `QueryTrace` / `QueryTrails` 建立 PR gate（例如：壓測 smoke test + latency regression 上限）。
2. 加入「大資料量 + 長時間 soak test」場景（30~60 分鐘），觀察 GC/記憶體曲線。
3. 若未來 audit trail 量級持續上升，可評估索引結構或 windowed cache 以降低查詢配置成本。
