# 回滾指南（Windows Server + Docker Desktop）

> 適用範圍：使用 `docker compose` 部署 bank-reporting-system（對應 `docs/DEPLOYMENT.md`）。

---

## 1. 回滾策略

### 1.1 目標
當新版本部署後出現故障，優先在最短時間恢復到上一個穩定版本，降低服務中斷時間。

### 1.2 觸發條件
符合任一情境即啟動回滾：
- `/health` 連續失敗
- 核心 API 5xx 持續發生
- 前端可開啟但關鍵流程不可用
- 上線後指標明顯惡化且無法在短時間內修復

### 1.3 基本原則
- 回滾版本以「上一個已驗證 tag/commit」為準
- 不改動 Git 歷史（禁止 force push）
- 先恢復服務，再做根因分析（RCA）

---

## 2. 回滾前檢查

```powershell
cd <repo-path>
docker compose ps
docker compose logs --tail=200 backend
docker compose logs --tail=200 frontend
curl http://<server-ip>:5000/health
```

建議先確認並記錄：
- 異常版本：`<bad-tag-or-commit>`
- 目標回滾版本：`<previous-stable-tag-or-commit>`
- 回滾開始時間與操作者

---

## 3. 標準回滾步驟

### Step 1：切換到穩定版本

```powershell
cd <repo-path>
git fetch --all --tags
git checkout <previous-stable-tag-or-commit>
```

### Step 2：重建並啟動容器

```powershell
docker compose up -d --build
```

> 如需完全重建可加 `--no-cache`：

```powershell
docker compose build --no-cache
docker compose up -d
```

### Step 3：驗證回滾結果

```powershell
docker compose ps
curl http://<server-ip>:5000/health
curl http://<server-ip>:5000/metrics
```

人工驗證至少包含：
1. 前端可正常開啟：`http://<server-ip>:8080/`
2. `/health` 成功
3. 核心流程（例：`/api/reports`）可正常使用

---

## 4. 回滾失敗處置

若回滾後仍異常，依序排查：
1. 檢查服務日誌
   - `docker compose logs --tail=200 backend`
   - `docker compose logs --tail=200 frontend`
2. 檢查主機資源（CPU / RAM / Disk）
3. 檢查網路與防火牆（8080/5000）
4. 回退到更早一版穩定版本

---

## 5. 回滾完成後紀錄與驗證

建議保留回滾紀錄：

```text
[Rollback Report]
- trigger: <故障描述>
- bad_version: <tag/commit>
- rollback_to: <tag/commit>
- start_time: <time>
- recovered_time: <time>
- health_check: pass/fail
- smoke_test: pass/fail
- owner: <name>
```

並於 PR / issue 補上：
- 故障摘要
- 回滾證據（health 與核心流程結果）
- 後續修復計畫
