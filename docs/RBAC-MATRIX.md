# RBAC Role Matrix (MVP)

## Scope
本文件定義目前系統角色在 API 層的最小權限矩陣，作為後續權限治理與測試依據。

## Roles
- `admin`: 系統管理（可存取所有 `/api/admin/*`）
- `superadmin`: 等同 admin（可由 `Authorization.AdminRoles` 設定）
- `reporter`: 申報作業使用者
- `viewer`: 唯讀檢視使用者

## API Permission Matrix

| API Scope | admin / superadmin | reporter | viewer |
|---|---|---|---|
| `/api/admin/*` | ✅ | ❌ | ❌ |
| `/api/declare` | ✅ | ✅ | ❌ |
| `/api/declare/result` | ✅ | ✅ | ✅ |
| `/api/reports*` | ✅ | ✅ | ✅ |
| `/api/news*` | ✅ | ✅ | ✅ |
| `/api/keys/*` | ✅ | ✅ | ❌ |
| `/api/token/update` | ✅ | ✅ | ❌ |

> 註：目前程式碼實作中，強制啟用的是 `/api/admin/*` 路徑權限；其餘路徑矩陣為下一階段 hardening 目標。

## Enforcement (Current)
- Middleware: `AdminAuthorizationMiddleware`
- Config: `Authorization.AdminRoles`
- Header input: `X-Role`

## Test Coverage (Current)
- `MiddlewareTests.AdminAuthorizationMiddleware_AllowsConfiguredRole`
- `MiddlewareTests.AdminAuthorizationMiddleware_RejectsNonAdminRole`

## Next Hardening Steps
1. 將 `/api/keys/*`、`/api/token/update` 納入 policy-based role check
2. 將 `X-Role` header 改為簽名 token claims（避免 header 偽造）
3. 增加 role matrix 對應 integration tests
