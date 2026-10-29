# AGENTS.md

Three independent deployment tracks in one repo:
`infra/terraform` (shared AWS) → `backend/restaurant-backend-app` (.NET Lambdas
via AWS Syndicate) → `frontend/restaurant-frontend-app` (Angular 22 → S3).

## Frontend
Read `frontend/restaurant-frontend-app/CLAUDE.md` first — it is the source of
truth for all frontend work (commands, conventions, test gates, traps). Do not
duplicate or contradict it here. Two corrections to it:
- The `@app/*` path alias EXISTS now (`tsconfig.json` paths) and is the dominant
  import style; the "no baseUrl/paths" claim in its §5 is stale.
- Region claims (us-west-1 / eu-west-1) are stale — see AWS notes below.

## Backend (.NET 10, AWS Lambda — cannot run locally)
- Solution: `restaurant-backend-app.sln`; lambdas under `dnapp/lambdas/`:
  `api-handler` (single API-Gateway entry, routes in
  `Routing/RouteRegistry.cs`), `reports-handler`, `report-sender`.
- API contract source of truth: `docs/ApiRoutes.md` + `RouteRegistry.cs` —
  they win over frontend models.
- Build/tests: `dotnet build restaurant-backend-app.sln`,
  `dotnet test Tests/APIHandler.Tests.csproj` (xunit + Moq + AutoFixture).
- Deploy (from `backend/restaurant-backend-app`), needs Python venv +
  `pip install aws-syndicate` and mandatory `SDCT_CONF` pointing at
  `.syndicate-config-restaurant-backend-app`:
  `syndicate build && syndicate deploy --deploy_name dev`
  (incremental: `syndicate update`). Full flow: root `README.md`.
- Config traps in `.syndicate-config-restaurant-backend-app/`: `project_path`
  is a hardcoded absolute path from another machine; bucket names contain a
  committed account ID. Both must be edited per environment.

## Infra (Terraform >= 1.10)
- `infra/terraform`: state in S3 (bucket must pre-exist before `init`);
  provisions frontend bucket, CloudFront, Cognito, SQS, EventBridge.
- Actual region is **eu-north-1** (`backend.tf`, `variables.tf`,
  `syndicate.yml`). The `us-west-2` commands in `README.md` and region claims
  in CLAUDE.md are stale — trust the config files.
- Verify order: `terraform fmt -check -recursive && terraform validate &&
  terraform plan` before apply.

## Repo-wide gotchas
- No CI (`.github/workflows/` is empty) — local verification is the only gate.
- Root `homework-backend.md`, `homework-frontend.md`, `practice-frontend.md`,
  `signals-migration.md` are training task briefs. They reference files/commands
  that do NOT exist yet (`homework-tests/`, `src/app/shared/mocks/`,
  group-orders models, `npm run test:hw`). Don't assume them; `signals-migration.md`
  is the agreed direction for NEW frontend code only.
- Use `npm ci`, never `npm install`. Production frontend build requires
  `BACKEND_URL` env var (`prebuild` hook generates
  `src/environments/environment.production.ts`; never commit it).
- `.github/skills/pptx/` and root `package-lock.json` are unrelated leftovers.
