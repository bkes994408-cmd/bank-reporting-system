# 銀行監理資料數位申報系統

Banking Regulatory Data Digital Reporting System (BRDRS)

## 📋 專案概述

此系統是為金融機構設計的銀行監理資料數位申報平台，提供報表申報、查詢、金鑰管理等功能。系統包含 C# .NET 8 後端 API 和 Vue 3 前端介面。

## 🏗️ 技術架構

### 後端
- **框架**: .NET 8 Web API
- **測試框架**: xUnit + Moq
- **依賴注入**: 內建 DI 容器

### 前端
- **框架**: Vue 3 + Vite
- **路由**: Vue Router 4
- **狀態管理**: Pinia
- **HTTP 客戶端**: Axios

## 📁 專案結構

```
bank-reporting-system/
├── backend/                    # .NET 8 後端
│   ├── Controllers/            # API 控制器
│   │   ├── DeclareController.cs
│   │   ├── KeysController.cs
│   │   ├── NewsController.cs
│   │   ├── ParsingController.cs
│   │   ├── ReportsController.cs
│   │   ├── SystemController.cs
│   │   ├── MonitoringController.cs
│   │   └── TokenController.cs
│   ├── DTOs/                   # 資料傳輸物件
│   │   └── RequestDTOs.cs
│   ├── Models/                 # 資料模型
│   │   └── ApiModels.cs
│   ├── Services/               # 服務層
│   │   ├── AgentService.cs
│   │   └── MonitoringService.cs
│   ├── Program.cs
│   ├── appsettings.json
│   └── BankReporting.Api.csproj
├── backend.tests/              # 後端測試
│   ├── ControllersTests.cs
│   └── BankReporting.Tests.csproj
├── frontend/                   # Vue 3 前端
│   ├── src/
│   │   ├── assets/
│   │   │   └── main.css
│   │   ├── components/
│   │   ├── router/
│   │   │   └── index.js
│   │   ├── services/
│   │   │   └── api.js
│   │   ├── stores/
│   │   ├── views/
│   │   │   ├── HistoryView.vue
│   │   │   ├── MonthlyView.vue
│   │   │   ├── NewsView.vue
│   │   │   ├── QueryView.vue
│   │   │   ├── SettingsView.vue
│   │   │   ├── SystemView.vue
│   │   │   └── UploadView.vue
│   │   ├── App.vue
│   │   └── main.js
│   ├── index.html
│   ├── package.json
│   └── vite.config.js
├── BankReporting.sln
└── README.md
```

## 🚀 快速開始

### 系統需求
- .NET 8 SDK
- Node.js 18+
- npm（建議使用 `npm ci` 搭配 `frontend/package-lock.json`）

### 後端啟動

```bash
# 進入後端目錄
cd backend

# 還原套件
dotnet restore

# 執行開發伺服器
dotnet run

# 後端將於 http://localhost:5000 執行
```

### 前端啟動

```bash
# 進入前端目錄
cd frontend

# 安裝依賴（依 lockfile 安裝）
npm ci

# 啟動開發伺服器
npm run dev

# 前端將於 http://localhost:5173 執行
```

### 執行測試

```bash
# 執行後端測試
cd backend.tests
dotnet test

# 執行前端 e2e 測試（Playwright）
cd ../frontend
npm ci
npx playwright install chromium
npm run test:e2e
```

### Docker 一鍵啟動（API + Web）

```bash
# 生產模式（後端 + 前端 Nginx）
docker compose up -d --build

# 開發模式（後端 + 前端 Vite dev server）
docker compose --profile dev up -d --build
```

- 生產模式前端：`http://localhost:8080`
- 開發模式前端：`http://localhost:5173`
- 後端 API：`http://localhost:5000`

停止服務：

```bash
docker compose down
```

## 📚 API 端點

| 方法 | 路徑 | 說明 |
|------|------|------|
| POST | `/api/parsing/excel` | Excel 轉 JSON |
| POST | `/api/parsing/excel-with-contact` | Excel + 聯絡人轉 JSON |
| POST | `/api/declare` | 上傳申報表 |
| POST | `/api/declare/result` | 查詢上傳結果 |
| POST | `/api/reports` | 查詢當月應申報報表 |
| POST | `/api/reports/histories` | 查詢報表申報歷程 |
| POST | `/api/reports/secure-archive/report-histories` | 加密封存報表歷程匯出資料 |
| POST | `/api/reports/secure-archive/declare-result` | 加密封存申報結果匯出資料 |
| POST | `/api/reports/secure-archive/query` | 查詢加密封存紀錄（遮罩 metadata） |
| GET | `/api/integrations/third-party/systems` | 取得可用第三方整合系統 |
| POST | `/api/integrations/third-party/sync` | 對外同步（含 retry/backoff） |
| GET | `/api/integrations/third-party/dead-letters` | 查詢同步死信佇列 |
| POST | `/api/integrations/third-party/dead-letters/{deadLetterId}/retry` | 手動重送死信佇列項目 |
| POST | `/api/compliance/regulations/snapshots` | 寫入法規文件快照（供後續比對） |
| POST | `/api/compliance/regulations/impact-analysis/generate` | 產生最新法規異動影響分析 |
| POST | `/api/compliance/regulations/impact-analysis/query` | 查詢法規影響分析報告 |
| POST | `/api/compliance/external-data/sync` | 同步外部合規平台風險數據（制裁/PEP 等） |
| POST | `/api/compliance/external-data/screen` | 以客戶資訊進行外部風險名單比對 |
| POST | `/api/compliance/financial-data/snapshots/upsert` | 寫入外部即時金融市場快照（MVP-7） |
| POST | `/api/compliance/financial-data/snapshots/query` | 查詢外部即時金融市場快照（MVP-7） |
| POST | `/api/compliance/predictive-risk/assess` | 生成預測性合規風險評估（Iteration-1） |
| POST | `/api/compliance/predictive-risk/query` | 查詢預測性合規風險評估結果 |
| POST | `/api/compliance/intelligent-reports/auto-submit` | 自動生成標準化報表並提交監管機構（支援 dry-run） |
| POST | `/api/compliance/intelligent-reports/query` | 查詢智能報表自動提交紀錄 |
| POST | `/api/compliance/blockchain/anchors/commit` | 寫入區塊鏈稽核錨點（探索） |
| POST | `/api/compliance/blockchain/anchors/query` | 查詢區塊鏈稽核錨點（探索） |
| POST | `/api/compliance/blockchain/sharing/simulate` | 模擬區塊鏈資料共享方案（探索） |
| POST | `/api/keys/import` | 匯入金鑰 |
| POST | `/api/keys/validate` | 驗證金鑰 |
| POST | `/api/token/update` | 更新 Token |
| POST | `/api/crypto/jwe/encrypt` | 直接產生 JWE payload（不依賴代理程式） |
| GET | `/api/check-version` | 檢查版本 |
| GET | `/api/info` | 查詢代理程式資訊 |
| POST | `/api/news` | 查詢公告 |
| POST | `/api/news/attachments` | 下載公告附件 |
| GET | `/api/settings` | 取得系統設定 |
| POST | `/api/settings` | 更新系統設定 |
| GET | `/health` | 健康檢查（服務存活與版本） |
| GET | `/metrics` | Prometheus 格式監控指標 |

### `/api/compliance/financial-data/snapshots/upsert` 契約（MVP-7）

- Request body
  - `sourceName`: 數據源名稱（例如：`twse-realtime-feed`、`cme-market-feed`）
  - `capturedAtUtc`: 快照時間（ISO8601，可省略，預設為伺服器當下 UTC）
  - `volatilityIndex`: 波動率指數（例如 VIX）
  - `creditSpreadBps`: 信用利差（bps）
  - `fxVolatilityPercent`: 外匯波動率（%）
  - `liquidityStressLevel`: `low|medium|high`
  - `metadata`: 其他來源欄位（可選）

- Predictive Risk 整合
  - `predictive-risk/assess` 會自動抓取最近 24 小時內最新快照，新增 `real_time_market_stress` 因子
  - 若 24 小時內無可用快照，評估流程會退回既有歷史稽核 + 法規變動訊號，不會報錯

### `/api/compliance/external-data/sync` 契約（MVP-6）

- Content-Type: `application/json`
- 必填欄位：
  - `providerName`: 外部供應商名稱（需對應 `ExternalComplianceData:Providers[].Name`）
- 選填欄位：
  - `datasetType`: 風險資料類型（預設 `sanctions`）
  - `pathOverride`: 暫時覆寫抓取路徑
  - `fieldMappings`: 欄位映射（**key/value 皆會以 case-insensitive 方式處理**）

請求範例：

```json
{
  "providerName": "kyc-aml-provider",
  "datasetType": "sanctions",
  "fieldMappings": {
    "Name": "entity_name",
    "RISKLEVEL": "severity",
    "country": "jurisdiction",
    "tags": "tag_list"
  }
}
```

成功回應範例（200）：

```json
{
  "code": "0000",
  "msg": "外部風險數據同步成功",
  "payload": {
    "providerName": "kyc-aml-provider",
    "datasetType": "sanctions",
    "importedCount": 128,
    "skippedCount": 4,
    "syncedAtUtc": "2026-03-11T09:21:40Z"
  }
}
```

### `/api/compliance/external-data/screen` 契約（MVP-6）

- Content-Type: `application/json`
- 必填欄位：
  - `customerName`
- 選填欄位：
  - `country`
  - `datasetType`

請求範例：

```json
{
  "customerName": "John Doe",
  "country": "TW",
  "datasetType": "sanctions"
}
```

成功回應範例（200）：

```json
{
  "code": "0000",
  "msg": "風險比對完成",
  "payload": {
    "customerName": "John Doe",
    "country": "TW",
    "totalMatches": 1,
    "suggestedDecision": "review",
    "matches": [
      {
        "recordId": "risk-20260311092000-8c7b6e7d74f04f5d",
        "providerName": "kyc-aml-provider",
        "datasetType": "sanctions",
        "name": "JOHN DOE",
        "country": "TW",
        "riskLevel": "high",
        "score": 0.91,
        "tags": ["sanction", "pep"]
      }
    ]
  }
}
```

### `/api/compliance/blockchain/anchors/commit` 契約（MVP-6 探索）

- Content-Type: `application/json`
- 必填欄位：無（會使用預設值）
- 常用欄位：
  - `anchorType`（預設 `audit_trail`）
  - `network`（預設 `sandbox-ledger`）
  - `auditTrailIds`（欲上鏈的稽核軌跡識別）
  - `summary`（本次錨點摘要）

請求範例：

```json
{
  "anchorType": "audit_trail",
  "network": "sandbox-ledger",
  "summary": "nightly compliance checkpoint",
  "auditTrailIds": ["trail-001", "trail-002"]
}
```

成功回應範例（200）：

```json
{
  "code": "0000",
  "msg": "區塊鏈稽核錨點寫入成功（探索）",
  "payload": {
    "anchorId": "anchor-20260312090000-9eaf2a6f3cbf",
    "anchorType": "audit_trail",
    "network": "sandbox-ledger",
    "anchorHash": "...",
    "previousAnchorHash": "..."
  }
}
```

### `/api/compliance/audit-trails/behavior-insights` 契約（MVP-6）

- Content-Type: `application/json`
- 常用欄位：
  - `startDateUtc` / `endDateUtc`（可選，預設近 7 天）
  - `topUsers`（預設 5，範圍 1~20）
  - `topPaths`（預設 8，範圍 1~20）

請求範例：

```json
{
  "startDateUtc": "2026-03-10T00:00:00Z",
  "endDateUtc": "2026-03-12T00:00:00Z",
  "topUsers": 5,
  "topPaths": 8
}
```

### `/api/compliance/audit-trails/trace` 契約（MVP-6）

- Content-Type: `application/json`
- 欄位：
  - `traceId`（可選，指定單一追溯鏈路）
  - `user`（可選，篩選使用者）
  - `startDateUtc` / `endDateUtc`（可選）
  - `maxSteps`（預設 20，範圍 1~200）

請求範例：

```json
{
  "traceId": "trace-20260312091500-abc123",
  "maxSteps": 50
}
```

### `/api/compliance/audit-trails/query` 進階查詢欄位（MVP-6）

新增可選欄位：

- `sensitiveOnly`：僅回傳敏感操作
- `minStatusCode` / `maxStatusCode`：依 HTTP status code 範圍篩選
- `minDurationMs`：依最小耗時篩選

### `/api/compliance/alerts/rules` 重要欄位說明

- `ruleType` 支援：`failed_requests`、`high_risk_operations`、`off_hours_sensitive`
- `sensitiveOnly` 行為：
  - `true`：先篩選 `isSensitiveOperation=true` 的稽核紀錄，再套用規則條件
  - `false`：對所有稽核紀錄套用規則條件
- 因此 `failed_requests` 與 `high_risk_operations` 在 `sensitiveOnly=true` 時，只會以敏感操作計算觸發門檻。

### `ExternalComplianceData:Providers` 設定範例

在 `backend/appsettings.json` 可設定多個資料源：

```json
{
  "ExternalComplianceData": {
    "Providers": [
      {
        "Name": "kyc-aml-provider",
        "BaseUrl": "https://compliance.example.com",
        "FetchPath": "/api/v1/risk-lists/sanctions",
        "ApiKey": "${COMPLIANCE_API_KEY}",
        "Enabled": true,
        "TimeoutSeconds": 15
      }
    ]
  }
}
```

## 📈 監控與告警（MVP 最小集合）

- 後端會記錄每筆 API 請求的 method/path/status/duration。
- 提供 `GET /metrics`，輸出 Prometheus 文字格式指標：
  - `bank_reporting_requests_total`
  - `bank_reporting_errors_total`（HTTP 5xx）
  - `bank_reporting_request_duration_ms_avg`
  - `bank_reporting_route_requests_total`
  - `bank_reporting_route_errors_total`
- 基本告警（以 log warning 輸出）：
  - 發生 HTTP 5xx
  - 單筆請求延遲 >= 2000ms

### `/api/parsing/excel` 契約（MVP）

- Content-Type: `multipart/form-data`
- 欄位：
  - `reportId`：`string`（可選，原樣回傳）
  - `uploadFile`：`.xlsx`（必填）
- Excel 結構：
  - 解析第一個工作表
  - 第一列作為 `headers`
  - 第二列起作為 `rows`

成功回應（200）：

```json
{
  "code": "0000",
  "msg": "解析成功",
  "payload": {
    "reportId": "AI302",
    "sheetName": "Sheet1",
    "headers": ["account", "amount"],
    "rows": [
      { "account": "現金", "amount": "1000" }
    ],
    "rowCount": 1
  }
}
```

錯誤碼：

- `PARSING_4001`：缺少檔案
- `PARSING_4002`：不支援檔案類型（僅 `.xlsx`）
- `PARSING_4003`：檔案為空
- `PARSING_4221`：Excel 結構不正確或不是合法 `.xlsx`
- `PARSING_4222`：工作表缺少標題列
- `PARSING_5000`：其他未預期錯誤

### `/api/declare` 契約（MVP）

- Content-Type: `application/json`
- 必填欄位：
  - `requestId`, `bankCode`, `bankName`, `reportYear`, `reportMonth`, `reportId`
  - `contractorName`, `contractorTel`, `contractorEmail`
  - `managerName`, `managerTel`, `managerEmail`
- 申報內容：`report` 與 `jwePayload` 至少需提供一個
- 簽章/JWE（選填）：
  - `useSignature: boolean`，為 `true` 時需提供 `signature`
  - `useJwe: boolean`，為 `true` 時需提供 `jwePayload`
- 若尚未有 `jwePayload`，可先呼叫 `POST /api/crypto/jwe/encrypt` 以本系統直接加密產生。

### `/api/crypto/jwe/encrypt` 契約（MVP-4）

- Content-Type: `application/json`
- 必填欄位：
  - `payload`: `object`（要加密的 JSON 內容）
  - `publicKeyPem`: `string`（RSA Public Key, PEM）
- 選填欄位：
  - `keyId`: `string`
- 成功回傳：
  - `payload.jwePayload`: compact JWE 字串
  - `payload.alg`: `RSA-OAEP-256`
  - `payload.enc`: `A256GCM`
### `/api/declare/result` 契約（MVP）

- Content-Type: `application/json`
- 請求欄位（至少需提供一個）：
  - `requestId`：`string`（申報請求編號）
  - `transactionId`：`string`（交易編號）

請求範例：

```json
{
  "requestId": "0070000-123"
}
```

成功回應（200）：

```json
{
  "code": "0000",
  "msg": "查詢成功",
  "payload": {
    "requestId": "0070000-123",
    "transactionId": "tx-001",
    "status": "SUCCESS",
    "message": "上傳完成"
  }
}
```

請求驗證失敗（400）：

```json
{
  "code": "4000",
  "msg": "requestId 或 transactionId 至少需填一個"
}
```
## 📊 支援的報表類型

| 報表編號 | 名稱 |
|---------|------|
| AI302 | 資產負債表 |
| AI330 | 授信擔保品別分析表 |
| AI335 | 大額授信資料表 |
| AI341 | 逾期放款統計表 |
| AI345 | 逾期放款資料表 |
| AI346 | 逾期放款結構分析表 |
| AI370 | 聯合授信個案資料表 |
| AI372 | 聯合授信額度資料表 |
| AI395 | 不動產放款資料表 |
| AI397 | 購屋貸款資料表 |
| AI501 | 存放款利率表 |
| AI505 | 存款結構分析表 |
| AI515 | 放款結構分析表 |
| AI520 | 利率敏感度缺口表 |
| AI555 | 消費性放款資料表 |
| AI560 | 信用卡業務資料表 |
| AI812 | 資本適足率報表 |
| AI813 | 槓桿比率表 |
| AI814 | 流動性覆蓋比率表 |
| AI823 | 淨穩定資金比率表 |
| AI863 | 資產品質分析表 |

## 🔐 安全性說明

- 申報流程可透過代理程式處理 JWE，或使用 `POST /api/crypto/jwe/encrypt` 由本系統直接產生 JWE
- Token 和金鑰需妥善保管，定期更新
- 建議使用 HTTPS 進行通訊

## ⚙️ 環境變數設定

### 後端 (appsettings.json)
```json
{
  "AgentSettings": {
    "BaseUrl": "https://127.0.0.1:8005/APBSA",
    "AutoUpdateTime": "03:00"
  }
}
```

### 前端 (vite.config.js)
```javascript
proxy: {
  '/api': {
    target: 'http://localhost:5000',
    changeOrigin: true
  }
}
```

## 📝 開發注意事項

1. **金融機構代碼**: 7位數代碼，格式為銀行3碼+0000（如：0070000）
2. **報表年度**: 使用民國年格式（如：113）
3. **報表月份**: 月報表填01~12，季報表填01~04

## 🐛 常見問題

### Q: 前端無法連接後端 API？
A: 請確認後端伺服器已啟動，並檢查 vite.config.js 中的 proxy 設定。

### Q: 金鑰驗證失敗？
A: 請確認已正確匯入金鑰A和金鑰B，並確保 Token 未過期。

### Q: 如何查看 API 文件？
A: 啟動後端後，訪問 http://localhost:5000/swagger 查看 Swagger UI。

## 📄 授權

此專案為內部使用，版權所有。

## 🤝 貢獻

如有問題或建議，請聯繫系統管理員。

---

**版本**: 1.0.0  
**最後更新**: 2026年2月
