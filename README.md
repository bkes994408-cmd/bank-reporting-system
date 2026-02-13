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
│   │   └── TokenController.cs
│   ├── DTOs/                   # 資料傳輸物件
│   │   └── RequestDTOs.cs
│   ├── Models/                 # 資料模型
│   │   └── ApiModels.cs
│   ├── Services/               # 服務層
│   │   └── AgentService.cs
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
- npm 或 pnpm

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

# 安裝依賴
npm install

# 啟動開發伺服器
npm run dev

# 前端將於 http://localhost:5173 執行
```

### 執行測試

```bash
# 執行後端測試
cd backend.tests
dotnet test
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
| POST | `/api/keys/import` | 匯入金鑰 |
| POST | `/api/keys/validate` | 驗證金鑰 |
| POST | `/api/token/update` | 更新 Token |
| GET | `/api/check-version` | 檢查版本 |
| GET | `/api/info` | 查詢代理程式資訊 |
| POST | `/api/news` | 查詢公告 |
| POST | `/api/news/attachments` | 下載公告附件 |
| GET | `/api/settings` | 取得系統設定 |
| POST | `/api/settings` | 更新系統設定 |

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

- 所有 API 請求需透過代理程式進行 JWE 加密
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
