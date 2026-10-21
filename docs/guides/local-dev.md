# Local development: Docker, Postgres, connection strings

How the API, Postgres, Docker and OrbStack fit together on your Mac, and the commands you use day to day.

---

## Prerequisites

- **OrbStack running.** It is the Docker engine on this Mac. Open it from Applications or run `orb start`
  (Settings → Start at login makes this automatic). If it isn't running, every `docker` command fails with
  `failed to connect to the docker API at unix:///Users/hakim/.orbstack/run/docker.sock`.
- .NET 10 SDK (pinned by `global.json`), `dotnet tool restore` once for `dotnet-ef`.

---

## First-time setup

```bash
cp .env.example .env                       # then put real values in .env
docker compose up -d db                    # pulls postgres:16-alpine the first time, then starts it
docker compose ps                          # wait for db "(healthy)"
dotnet user-secrets set "ConnectionStrings:Default" \
  "Host=localhost;Port=5432;Database=senabayu;Username=<DB_USER>;Password=<DB_PASSWORD>" \
  --project Homestay.Api
```

`docker compose up` does everything itself: reads `compose.yml` + `.env`, pulls the image if it isn't cached,
creates the network and volume, starts the container. The only thing it can't do is start the Docker engine —
that's OrbStack's job.

---

## How it's connected

```
 Your Mac
 ┌──────────────────────────────────────────────────────────────┐
 │                                                              │
 │  Rider: Homestay.Api (dotnet process)                        │
 │     │  Host=localhost;Port=5432                              │
 │     ▼                                                        │
 │  localhost:5432 ─── port mapping "5432:5432" ───┐            │
 │                                                 │            │
 │  OrbStack (Docker engine, a small Linux VM)     │            │
 │  ┌──────────────────────────────────────────────▼─────────┐  │
 │  │ network: senabayu-api_default                          │  │
 │  │                                                        │  │
 │  │  container "db" (from image postgres:16-alpine)        │  │
 │  │     Postgres listening on 5432                         │  │
 │  │     /var/lib/postgresql/data ──► volume postgres_data  │  │
 │  │                                                        │  │
 │  │  container "api" (S5, from your Dockerfile)            │  │
 │  │     Host=db;Port=5432 ──► reaches "db" by name         │  │
 │  └────────────────────────────────────────────────────────┘  │
 └──────────────────────────────────────────────────────────────┘
```

| Piece | What it is |
|---|---|
| **Docker engine** | The program that actually runs containers. On a Mac it lives in a small Linux VM; OrbStack provides it. The `docker` command is only a client that sends instructions to the engine over a socket. |
| **Image** | A read-only template. `postgres:16-alpine` = Postgres 16 on minimal Alpine Linux, downloaded once from Docker Hub and cached. |
| **Container** | A running instance of an image, with its own filesystem and network. `postgres_db` is a real Postgres server. |
| **Volume** | Storage outside the container. The container's own filesystem is thrown away when it's removed; the data survives because `/var/lib/postgresql/data` is stored in the `postgres_data` volume. `docker compose down` keeps it; `docker compose down -v` deletes it. |
| **Port mapping** `"5432:5432"` | `"Mac port:container port"` — opens a door from your Mac into the container. Without it, Rider couldn't reach Postgres. |
| **Compose network** | Compose puts all services in one private network where they find each other **by service name**. That's why the API container uses `Host=db`. Inside a container, `localhost` means that container itself. |
| **Environment variables** | `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` tell the Postgres image what to create — **on first start only**. Compose fills them in from `.env`. |

**Gotcha:** changing `DB_USER` or `DB_PASSWORD` in `.env` after the first start does nothing, because the volume
already holds an initialized database. To start over: `docker compose down -v`, then `docker compose up -d db`
(this wipes the data).

---

## Connection strings: one per place the API runs

The API always reads the same key, `ConnectionStrings:Default`. Only the source and the host change.

| Where the API runs | Value comes from | Host |
|---|---|---|
| **Rider on your Mac** (day to day) | user-secrets | `localhost` |
| **Docker container** (M1 S5) | `compose.yml` env var built from `.env` | `db` |
| **Azure** (M1 S6) | Container Apps secret | `<server>.postgres.database.azure.com`, plus `SSL Mode=Require` |

- **`.env` is for Docker Compose only.** ASP.NET Core doesn't read `.env` files.
- **user-secrets** are stored in `~/.microsoft/usersecrets/<UserSecretsId>/`, outside the repo, and loaded
  automatically when `ASPNETCORE_ENVIRONMENT=Development`. The `UserSecretsId` in the csproj is just an ID and is
  safe to commit. Check what's set with `dotnet user-secrets list --project Homestay.Api`.
- **`ConnectionStrings__Default`**: environment variable names can't contain `:`, so ASP.NET Core turns `__`
  into `:`. The env var `ConnectionStrings__Default` and the JSON key `ConnectionStrings:Default` are the same
  setting.
- **Precedence** (later wins): `appsettings.json` → `appsettings.{Environment}.json` → user-secrets
  (Development only) → environment variables → command-line args.
- The production connection string never goes in the repo — only in the Container Apps secret.

On Azure none of the local machinery exists: no compose, no OrbStack, no volume. The API image runs on Container
Apps and Postgres is a managed server Azure runs (with backups). Only the connection string changes — which is
why it's configuration, not code.

---

## Day-to-day commands

```bash
docker compose up -d db                              # start Postgres in the background
docker compose ps                                    # status; db should be "(healthy)"
docker compose logs -f db                            # follow Postgres logs
docker compose exec db psql -U <DB_USER> -d senabayu # SQL prompt inside the container (\q to quit)
docker compose stop db                               # stop without removing (e.g. to test /health/ready)
docker compose down                                  # stop + remove containers; data stays in the volume
docker compose down -v                               # same, and delete the data

dotnet ef migrations add <Name> --project Homestay.Api  # new migration after a model change
dotnet ef database update --project Homestay.Api        # apply migrations (+ seeding) to the local db
```

Rider's Database panel: + → Data Source → PostgreSQL, host `localhost`, port `5432`, your `DB_USER` /
`DB_PASSWORD`, database `senabayu`.
