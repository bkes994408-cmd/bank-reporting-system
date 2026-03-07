# RBAC Testing Guide

## Purpose
快速驗證不同角色（`admin` / `superadmin` / `reporter` / `viewer`）在前端與 API 的權限行為是否符合預期。

## Frontend Manual Check
1. 打開「設定」頁（`/settings`）
2. 在「目前測試角色」切換 active role
3. 到對應頁面操作 API（例如後台管理、token 更新、keys 匯入）
4. 觀察是否被允許或回傳 403

## Expected Behavior
- `admin` / `superadmin`
  - 可存取 `/api/admin/*`
  - 可執行 `/api/keys/*` 與 `/api/token/update`
- `reporter`
  - 不可存取 `/api/admin/*`
  - 可執行 `/api/keys/*` 與 `/api/token/update`
- `viewer`
  - 不可存取 `/api/admin/*`
  - 不可執行 `/api/keys/*` 與 `/api/token/update`

## API Quick Test (curl)
```bash
# admin route should pass
curl -i -H 'X-Role: admin' http://localhost:5214/api/admin/users

# admin route should fail for reporter
curl -i -H 'X-Role: reporter' http://localhost:5214/api/admin/users

# operator route should pass for reporter
curl -i -X POST -H 'Content-Type: application/json' -H 'X-Role: reporter' \
  -d '{"token":"demo"}' http://localhost:5214/api/token/update
```

## Notes
- 目前 `X-Role` 為測試用途 header，正式環境應改為由簽名 token claims 提供。
