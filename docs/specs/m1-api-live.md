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
How the pieces fit together, and the day-to-day commands: [`docs/guides/local-dev.md`](../guides/local-dev.md).

4. One `compose.yml` (no override file — production is Azure, not compose, so this file is local-only).
   Postgres: pinned `postgres:16-alpine` (Azure gets 16 too), **named volume**, `pg_isready` healthcheck,
   `restart: unless-stopped`, credentials from `.env`, port `${DB_PORT}:5432`.
5. `.env.example` with placeholder values for `DB_USER`, `DB_PASSWORD`, `DB_NAME`, `DB_PORT`; real values in
   `.env` (git-ignored).
6. Connection string for running from Rider in **user-secrets**, never in a committed file:
   `dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=senabayu;Username=<DB_USER>;Password=<DB_PASSWORD>" --project Homestay.Api`
7. Verify: `docker compose ps` shows the db `(healthy)`; data survives `docker compose down && docker compose up -d`.

Status 2026-10-21: Postgres running, user-secret set. Still to do: healthcheck + `unless-stopped` in
`compose.yml`, placeholder values in `.env.example`, commit.

### S3 — Domain, EF Core, first endpoint (≈3 h)
8. Packages — all on the 10.0.x line, no prereleases (latest stable as of 2026-10-21):
   ```bash
   dotnet add Homestay.Api package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
   dotnet add Homestay.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.13
   dotnet add Homestay.Api package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 10.0.13
   ```
9. `Domain/` — plain classes: **no `using Microsoft.EntityFrameworkCore`, no `[Key]`/`[Required]` attributes**.
   - `RoomType`: `int Id`, `string Code` (e.g. `"DELUXE"`), `NameMs`, `NameEn`, `DescriptionMs`,
     `DescriptionEn`, `decimal BasePricePerNight`, `int MaxGuests`, `int MinNights`,
     `decimal ExtraGuestFeePerNight`, `bool IsActive`, `List<Room> Rooms`.
   - `Room`: `int Id`, `int RoomTypeId`, `RoomType RoomType`, `string Number`, `bool IsActive`.
   - Money is `decimal`, never `double`.
10. `Data/` — all EF mapping lives here:
    - `AppDbContext` with `DbSet<RoomType>`, `DbSet<Room>`; `OnModelCreating` calls
      `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
    - `Data/Configurations/RoomTypeConfiguration.cs` (`IEntityTypeConfiguration<RoomType>`): unique index on
      `Code`, `HasPrecision(10, 2)` on money, `IsRequired()` + `HasMaxLength(...)` on strings.
    - `Data/Configurations/RoomConfiguration.cs`: unique index on `Number`, the relationship to `RoomType`.
    - Same idea as `BookingSystem`'s `EntityConfigurations.cs`, one file per entity.
    - **Checkpoint: send `Domain/` + `Data/` for review before generating the migration.**
11. Register in `Program.cs`:
    ```csharp
    builder.Services.AddDbContext<AppDbContext>(o =>
        o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
    ```
12. Seed with EF Core's `UseSeeding` / `UseAsyncSeeding` in the same `AddDbContext` options. EF runs them when
    migrations are applied (`dotnet ef database update`, later the migrations bundle on Azure), not on every app
    start. Idempotent: return early if any `RoomTypes` exist. 3 room types (ms + en names/descriptions, MYR
    prices) and 5 rooms.
13. Migration:
    ```bash
    dotnet ef migrations add InitialCreate --project Homestay.Api
    ```
    Read the generated file: money columns `numeric(10,2)`, unique indexes on `Code` and `Number`, strings with
    lengths and `NOT NULL`. Then `dotnet ef database update --project Homestay.Api` and inspect the tables in
    Rider's Database panel (+ → Data Source → PostgreSQL, `localhost:5432`).
14. Endpoint:
    - `Dtos/RoomTypeDto.cs`: a `record` with `Code`, `Name`, `Description`, `BasePricePerNight`, `MaxGuests`,
      `MinNights` — `Name`/`Description` picked by locale.
    - `Controllers/RoomTypesController.cs`: `[ApiController]`, `[Route("api/v1/room-types")]`, `[HttpGet]`
      with `[FromQuery] string locale = "en"`; active only, ordered, mapped to the DTO. Inject `AppDbContext`
      directly — no business logic yet to justify a service.
    - DTOs, never entities: keeps internal fields and navigation properties out of the API, and the API's
      response shapes don't change every time the database does.
15. Health checks:
    ```csharp
    builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>(tags: ["ready"]);
    // ...
    app.MapHealthChecks("/health/live", new() { Predicate = _ => false });                  // process only
    app.MapHealthChecks("/health/ready", new() { Predicate = c => c.Tags.Contains("ready") }); // checks the db
    ```
    Test: `docker compose stop db` → `/health/ready` unhealthy, `/health/live` still healthy.

**S3 done when:** `GET /api/v1/room-types?locale=ms` in Swagger returns the seeded rooms with Malay names, and
both health endpoints behave as above.

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
18. Add `api` to `compose.yml`, building the connection string from the same `.env` variables:
    ```yaml
      api:
        build: .
        ports:
          - "8080:8080"
        environment:
          ConnectionStrings__Default: "Host=db;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD}"
        depends_on:
          db:
            condition: service_healthy
    ```
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
