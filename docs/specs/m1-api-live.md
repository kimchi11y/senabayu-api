# M1 Spec — API skeleton live on Azure

**Milestone:** M1 (≈12 h). **Repo:** `senabayu-api`. **Depends on:** nothing. **Blocks:** M2–M4.
**Goal:** by the end of this milestone a **public HTTPS URL serves your API**: seeded room types from Azure
Database for PostgreSQL, running as a container on Azure Container Apps, redeployed automatically by GitHub
Actions on every merge to `main`. Locally the same image runs under one `docker compose` command. No SPA, no
landing, no auth.

You write all of this. This spec is the acceptance contract; I review the PR against it.

> Rewritten 2026-10-21 for the three-repo split and controller-based API (`PLAN.md` decisions 1, 4, 19).
> Replaces the monorepo-era `m1-skeleton-api-live.md`.

---

## Where you are

Already done (commit `60df02c` + the uncommitted OpenAPI fix — commit that first):
- Solution `Homestay.slnx` with `Homestay.Api` (controllers, .NET 10), `.gitignore`, `.editorconfig`,
  `global.json`, `dotnet-tools.json` (`dotnet-ef`).
- `Microsoft.AspNetCore.OpenApi` 10.0.13 serving `/openapi/v1.json`, Swagger UI at `/swagger` in Development.

Layout note: the project lives at `Homestay.Api/` in the repo root (Rider's layout), not `src/Homestay.Api/` as
`PLAN.md` §4 shows. Keep it that way; put the test project at `Homestay.Tests/` next to it.

---

## Scope

In: `Domain/` + `Data/` + `Controllers/` + `Dtos/` folders, EF Core + Postgres, first migration, seed data,
`GET /api/v1/room-types`, Swagger, error contract, health endpoints, multi-stage Dockerfile, local compose
(api + db), Azure resources (budget alert, resource group, PostgreSQL Flexible Server, Container Apps environment +
app, custom domain + managed certificate), CI, CD via GitHub Actions + OIDC, `deploy/azure/README.md` runbook,
README stub.
Out: booking endpoints, availability/quote, `Services/` (nothing to put there yet), auth, admin, the SPA, the
landing, uploads, Bicep/Terraform, automated migrations in CD (M3), Application Insights, tests beyond the basics.

---

## Task list

### S1 — Repo hygiene (≈0.5 h)
1. Push the repo to GitHub as `kimchi11y/senabayu-api`. Move `PLAN.md` and this spec in if they aren't yet.
2. `.env.example` listing every variable (Postgres user/password/db, host port, connection string). `.env` is
   already ignored; never commit it.
3. Delete the WeatherForecast sample (`WeatherForecast.cs`, `WeatherForecastController.cs`, the `.http` entry)
   once your first real controller works.

### S2 — Postgres in Docker (≈1 h)
4. `compose.yml` (production-shaped) + `compose.override.yml` (dev). Postgres: pinned image tag (e.g.
   `postgres:17-alpine` — use the same major you'll pick on Azure), **named volume**, healthcheck
   (`pg_isready`), credentials from `.env`, port published only in the override file.
5. Day-to-day: `docker compose up db` and run the API from Rider against it.
6. Verify data survives `docker compose down && docker compose up`.

### S3 — Domain, EF Core, first endpoint (≈3 h)
7. Packages (all **10.0.x**, no prereleases): `Npgsql.EntityFrameworkCore.PostgreSQL`,
   `Microsoft.EntityFrameworkCore.Design`.
8. `Domain/`: `RoomType`, `Room`. Plain classes: **no EF attributes, no EF usings**.
9. `Data/`: `AppDbContext`, `IEntityTypeConfiguration<RoomType>` / `<Room>` with keys, unique indexes
   (`RoomType.Code`, `Room.Number`), money as `numeric(10,2)`, required fields. **Mapping lives here, not on the
   entities.** Connection string from configuration (`ConnectionStrings:Default`), development value in
   user-secrets or `appsettings.Development.json` pointing at the compose db — not a production value.
10. `dotnet ef migrations add InitialCreate`, then read the migration: money column type, unique indexes,
    nullability are what you intended.
11. Seeder: 3 room types with `NameMs`/`NameEn`/`DescriptionMs`/`DescriptionEn`, prices in MYR, and 5 rooms.
    Idempotent (safe to run twice). Run it from a dev-only path, not on every production start.
12. `Dtos/RoomTypeDto.cs` and `Controllers/RoomTypesController.cs`: `[ApiController]`,
    `[Route("api/v1/room-types")]`, `GET ?locale=ms|en` returning DTOs (never entities), active only, ordered.
    Controller injects `AppDbContext` directly for now — there's no business logic yet to justify a service.
13. Health checks: `GET /health/live` (process up) and `GET /health/ready` (checks Postgres — must actually fail
    when the db is down). Use `AddHealthChecks()` + the Npgsql/EF health check.

### S4 — Swagger + error contract (≈1 h)
14. Swagger UI must work **in production too** for the demo (move it out of the `IsDevelopment()` block, or gate
    it on a config flag). `/openapi/v1.json` likewise.
15. `AddProblemDetails()` + `UseExceptionHandler()`: an unhandled exception returns `500` with a `ProblemDetails`
    JSON body (with a stable `code` field), never an HTML page or stack trace.
16. `UseHttpsRedirection()`: remove it or skip it outside Development — Container Apps terminates TLS at the
    ingress and forwards plain HTTP to your container.

### S5 — Container image (≈1 h)
17. `Dockerfile` at the repo root: multi-stage (`mcr.microsoft.com/dotnet/sdk:10.0` build →
    `mcr.microsoft.com/dotnet/aspnet:10.0` runtime), cache-friendly (copy `.csproj` + restore before copying the
    rest), non-root (`USER $APP_UID`), listening on `8080` (`ASPNETCORE_HTTP_PORTS=8080`). Add a `.dockerignore`
    (`bin/`, `obj/`, `.git/`, `.idea/`, `.env`).
18. Add `api` to `compose.yml`: connection string interpolated from `.env` with `Host=db`, `depends_on` the db with
    `condition: service_healthy`.
19. Verify: `docker compose up --build` serves `http://localhost:8080/api/v1/room-types`; `docker compose exec api
    whoami` is not root; the runtime image has no SDK.

### S6 — Azure foundation + first deploy (≈3 h)
Record **every** `az` command you run, in order, in `deploy/azure/README.md`, with placeholders instead of secrets.

20. Activate **Azure for Students**. Before creating anything: **Cost Management → Budget** (e.g. US$20/month)
    with email alerts at 50 % and 90 %.
21. Install the Azure CLI + `containerapp` extension. Create resource group `rg-senabayu` in Southeast Asia (or the
    region your subscription's policy allows — a policy error names the allowed regions).
22. **PostgreSQL Flexible Server**: Burstable B1ms, smallest storage, same major version as local, public access,
    firewall rule for **your IP only** plus "allow Azure services". Create database `senabayu` and an app login
    that isn't the server admin. TLS is required (`SSL Mode=Require` in the connection string).
23. Apply the schema by hand: `dotnet ef migrations bundle`, run it against the Azure connection string from your
    machine. **No `Database.Migrate()` on app startup.** Seed once the same way (or a one-off command).
24. Push the image: `ghcr.io/kimchi11y/senabayu-api:<git-sha>`, then make the GHCR package **public**.
25. **Container Apps environment** (Consumption) + container app `senabayu-api`: GHCR image, target port `8080`,
    external ingress, **min replicas 0 / max 2**, connection string as a **Container Apps secret** referenced by
    `ConnectionStrings__Default=secretref:...`. Probes: liveness → `/health/live`, readiness → `/health/ready`.
26. Confirm on the `*.azurecontainerapps.io` URL that `/health/ready`, `/api/v1/room-types` and `/swagger` work
    **before** touching DNS. Read the logs once (`az containerapp logs show`).

### S7 — Custom domain (≈1 h)
27. Cloudflare DNS: **DNS-only (grey cloud)** `CNAME api.senabayu-demo → <app>.azurecontainerapps.io` plus the
    `TXT asuid.api.senabayu-demo` record Azure gives you.
28. Add the hostname to the container app and bind a **free managed certificate**. Keep the record grey.
29. Verify from outside: `https://api.senabayu-demo.hakimrazak.com/health/ready` and `/swagger`, valid certificate.

### S8 — CI, CD, README (≈2.5 h)
30. `Homestay.Tests` (xUnit) added to the solution with one trivial test, so CI has something to run. M3 fills it.
31. `.github/workflows/ci.yml` on pull requests and `main`: restore → build → test, plus `docker build` (no push).
32. **OIDC instead of secrets:** Entra app registration (or user-assigned managed identity) with a **federated
    credential** for `repo:kimchi11y/senabayu-api:ref:refs/heads/main`, granted **Contributor on `rg-senabayu`
    only**. Store `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as GitHub **variables**.
33. `.github/workflows/deploy.yml` on push to `main`: `permissions: id-token: write, packages: write, contents:
    read` → build + push `ghcr.io/...:<sha>` with `GITHUB_TOKEN` → `azure/login` (OIDC) →
    `az containerapp update --image ...:<sha>` → smoke test `curl --fail --retry 5` on `/health/ready`.
34. README stub: what it is, the architecture diagram from `PLAN.md`, how to run locally (the actual commands),
    the live API URL, link to `deploy/azure/README.md`. Rewritten properly in M4.

---

## Acceptance criteria

- [ ] Fresh clone → copy `.env.example` to `.env` → `docker compose up --build` serves the seeded room types locally.
- [ ] `GET https://api.senabayu-demo.hakimrazak.com/api/v1/room-types?locale=ms` returns seeded room types from
      Azure Postgres.
- [ ] `/swagger` reachable over HTTPS (valid managed certificate) and documents the endpoint and response shape.
- [ ] `/health/ready` fails when Postgres is unreachable (stop the db container locally) and recovers after.
- [ ] Seeded data survives `docker compose down && docker compose up` (named volume proof).
- [ ] Container runs as non-root; runtime image has no SDK.
- [ ] `Domain/` has no `using Microsoft.EntityFrameworkCore` and no EF attributes.
- [ ] Merging a trivial change to `main` produces a green `deploy.yml` run and a new revision on the new SHA, with
      no manual step.
- [ ] No Azure credential or connection string in the repo, GitHub secrets, or the image.
- [ ] A budget alert exists on the subscription.
- [ ] `deploy/azure/README.md` lists every command needed to recreate the Azure resources.
- [ ] CI green including `docker build`; `.env.example` documents everything.
- [ ] `dotnet ef migrations add Check` after the initial migration produces an empty migration (then delete it).

---

## Concepts you must be able to explain afterwards

- **ASP.NET Core:** controllers + `[ApiController]` (automatic 400s, binding sources), middleware order in
  `Program.cs`, DI lifetimes (why `DbContext` is scoped), configuration sources (appsettings → env vars →
  secrets, and why `ConnectionStrings__Default` maps to `ConnectionStrings:Default`).
- **Docker:** image vs container vs volume; multi-stage; non-root; why the local db needs a named volume.
- **Compose:** services, networks (why the API uses `Host=db`), `depends_on` conditions, override files, `.env`.
- **EF Core:** migrations vs `EnsureCreated`, why migrations don't run on startup in a scaled-out service, why
  mapping is configuration not attributes, why DTOs instead of entities in responses.
- **Azure:** resource group; Container Apps environment vs app vs revision; where TLS terminates; scale-to-zero
  and cold starts; liveness vs readiness; managed Postgres vs a Postgres container; secrets vs env vars.
- **CI/CD:** CI vs CD; OIDC federated credentials vs a stored secret, and why the role is scoped to one resource
  group; why images are tagged by commit SHA; how you'd roll back.

---

## Review checklist I'll apply to your PR

1. `Domain/` free of EF; all mapping in `Data/`.
2. Controllers thin: return DTOs, no entity leaks, correct status codes.
3. No secrets committed or in GitHub; `.env.example` complete; compose interpolates rather than hardcodes.
4. `/health/ready` reflects real readiness (db), and the Azure probes point at the right paths.
5. Dockerfile reproducible, non-root, no SDK shipped.
6. Money as `decimal`/`numeric`, never `double`.
7. Every Microsoft/EF package on the same 10.0.x line; no prereleases.
8. Migrations reviewed, never applied on startup.
9. Deploy workflow least-privilege and SHA-tagged.
10. `deploy/azure/README.md` matches what exists in the portal; README commands actually work.
