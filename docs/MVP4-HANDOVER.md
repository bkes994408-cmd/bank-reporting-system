# MVP-4 Handover Summary

## Delivered PRs
- #52: performance baseline + request monitoring middleware refactor
- #53: remove unused legacy `AccountAdminService`
- #54: MVP-4 iteration plan + RBAC matrix + CI branch trigger expansion
- #55: operator-role guard on sensitive write APIs (`/api/keys/*`, `/api/token/update`)
- #56: roadmap progress update
- #57: CI quality gate (`secret-scan`)
- #58: frontend role switch + unified `X-Role` header
- #59: RBAC testing guide
- #60: admin UI inline role editing
- #61: operator-role guard on `/api/declare`
- #62: admin UI role options visibility

## Current Security/RBAC State
- `/api/admin/*` requires admin role set (`Authorization.AdminRoles`)
- Sensitive write paths require operator role set (`Authorization.OperatorRoles`):
  - `POST /api/keys/*`
  - `POST /api/token/update`
  - `POST /api/declare`
- Frontend supports quick role simulation for QA via Settings page

## CI State
- CI includes `quality` gate before backend/frontend jobs
- `quality` currently runs tracked-file secret scan script

## Suggested Next Sprint (MVP-5 candidate)
1. Move from header-based role simulation to signed token claims
2. Add backend authorization integration tests for all protected routes
3. Add frontend E2E flow for role switch -> forbidden/allowed assertions
4. Add nightly perf smoke benchmark job
