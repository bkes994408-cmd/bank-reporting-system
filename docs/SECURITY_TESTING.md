# Security Testing（MVP-2）

本文件對應 issue #18，涵蓋以下重點：

1. **無 secrets（Hardcoded secrets）**
   - 以 `backend.tests/SecurityTests.cs` 進行靜態掃描測試。
   - 掃描 `.cs/.json/.md`，排除 `bin/obj/.git/node_modules`。
   - 檢查常見模式（AWS Key、GitHub PAT、Private Key、疑似 password/secret/apiKey literal）。

2. **日誌不含敏感資料**
   - Request logging 僅記錄 Method / Path / StatusCode / Duration。
   - 測試驗證 `Program.cs` 未記錄 `Authorization`、`Token`、`KeyA`、`KeyB` 等敏感欄位。

3. **輸入驗證**
   - `TokenController`：拒絕空白 token、限制長度，送出前 trim。
   - `KeysController`：拒絕空白 key，送出前 trim。
   - `NewsController.DownloadAttachment`：驗證 URL 必須為 HTTP/HTTPS 絕對網址、檔名必填、附件型別白名單。

## 測試執行

```bash
dotnet test
```

上述測試已納入既有測試專案中。