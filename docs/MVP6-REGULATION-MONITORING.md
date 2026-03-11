# MVP-6：自動化法規更新監測與影響分析

## 目標

建立可持續運作的法規變更監測與影響分析能力，將監管文件的非結構化內容轉為可操作的系統調整建議。

## 本次交付

- 新增法規快照寫入 API：`POST /api/compliance/regulations/snapshots`
- 新增影響分析生成 API：`POST /api/compliance/regulations/impact-analysis/generate`
- 新增影響分析查詢 API：`POST /api/compliance/regulations/impact-analysis/query`
- 新增 `RegulationMonitoringService`
  - 條文切分（line + `。`）
  - 條文鍵值抽取（條號/序號）
  - 新舊版本差異分析（added/removed/updated）
  - 規則式 impact mapping（申報流程/報表格式/數據採集/稽核留痕）
  - 建議動作生成（recommended actions）

## 設計重點

1. **版本比較基礎模型**
   - 以 `Source + DocumentCode` 聚合同一份法規
   - 每次分析取最新兩版快照比較

2. **智能解析（MVP 階段）**
   - 先以規則式 NLP（keyword + clause diff）實作
   - 後續可擴充 embedding 或 LLM 分析器以提升準確率

3. **影響輸出可執行**
   - 每個 impact area 都輸出 severity + reason
   - 附帶 recommended actions，方便落地成開發/合規待辦

## 後續建議

- 對接外部來源（FSC/CBC RSS 或公告 API）自動定時寫入 snapshot
- 增加 false positive 回饋機制，持續優化 impact 規則
- 影響分析結果串接 ticket 系統（Jira/GitHub Issues）自動建單
