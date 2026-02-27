# 回滾指南（Windows Server + Docker Compose）

> 適用目標：`docs/DEPLOYMENT.md` 所述部署方式（Windows Server + Docker Desktop + `docker compose`）。
>
> 本文件對應 `docs/ROADMAP.md` 的 MVP-3 項目：`回滾指南（docs/ROLLBACK.md）`。

---

## 1. 目的與觸發條件

當新版本部署後出現以下狀況時，應啟動回滾：

- 核心 API 持續失敗（HTTP 5xx 或關鍵流程不可用）
- 前端無法正常載入或與後端連線異常
- `/health` 不健康，且短時間內無法修復
- 監控或營運確認新版本影響業務

目標：**在最短時間恢復到上一個可用版本**，先止血、再排查。

---

## 2. 回滾前準備（建議）

每次部署前請先記錄：

1. 目前線上版本（Git commit / tag）
2. 前一個穩定版本（Git commit / tag）
3. 對應容器映像版本（若有版本標記）
4. 部署時間與操作者

建議在發版單或變更紀錄中保留：

```text
current=3c421ff
previous=9069b4a
deploy_time=2026-02-28T04:30:00+08:00
operator=<name>
```

---

## 3. 快速健康檢查（確認是否需回滾）

```powershell
# 查看容器狀態
cd <repo-path>
docker compose ps

# 查看最近錯誤日誌
docker compose logs --tail=200 backend
docker compose logs --tail=200 frontend

# 健康檢查（依實際主機調整）
curl http://<server-ip>:5000/health
```

若確認為新版本造成且無法快速修復，執行第 4 節回滾。

---

## 4. 標準回滾流程（Git 版本回退）

### Step 1：切換到上一穩定版本

```powershell
cd <repo-path>

# 更新遠端資訊
git fetch --all --tags

# 切換到上一穩定版本（建議 tag；次選 commit）
git checkout <previous-stable-tag-or-commit>
```

### Step 2：重新建置並啟動服務

```powershell
# 重新建置映像並在背景啟動
# 若需最乾淨重建可加 --no-cache

docker compose up -d --build
```

### Step 3：驗證回滾結果

```powershell
# 服務狀態
docker compose ps

# 健康端點
curl http://<server-ip>:5000/health

# 監控端點
curl http://<server-ip>:5000/metrics
```

人工驗證最少包含：

1. 前端可開啟（`http://<server-ip>:8080/`）
2. 後端 `/health` 正常回應
3. 至少 1 條核心流程（例如 `/api/reports`）可正常完成

---

## 5. 回滾失敗時的緊急處置

若回滾後服務仍異常，請依序處理：

1. 檢查容器啟動錯誤
   - `docker compose logs backend`
   - `docker compose logs frontend`
2. 確認主機資源（磁碟、記憶體）
3. 確認網路與防火牆埠（`8080` / `5000`）
4. 必要時回退到更早的已驗證版本

---

## 6. 回滾後必做事項

1. 建立事故紀錄（時間、影響範圍、根因初判）
2. 在 PR / issue 記錄回滾版本與驗證證據
3. 對失敗版本開立修復 issue，補測試後再重新發版

建議紀錄模板：

```text
[Rollback Report]
- trigger: <故障描述>
- bad_version: <tag/commit>
- rollback_to: <tag/commit>
- start_time: <time>
- recovered_time: <time>
- health_check: pass/fail
- api_smoke_test: pass/fail
- owner: <name>
```

---

## 7. 注意事項

- 回滾流程不應使用 `--force` 推送或覆寫歷史。
- 若版本含資料結構變更（DB migration），需先評估**資料可逆性**再回滾。
- 優先回復可用性，再進行根因分析（RCA）。
