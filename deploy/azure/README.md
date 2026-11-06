# Azure runbook

Every command used to create the Azure resources, in order. Secrets are placeholders; never commit real values.

Subscription: Azure for Students (spending limit on). Its policy allows only `koreacentral`, `malaysiawest`,
`indiasouthcentral`, `japaneast` and `eastasia`. Everything here is in `malaysiawest`.

## 1. Budget alert

US$20/month, emailing the account owner at 50 % and 90 % of actual spend. Replace `<subscription-id>`, and put the
alert email in `budget.json`:

```json
{
  "properties": {
    "category": "Cost",
    "amount": 20,
    "timeGrain": "Monthly",
    "timePeriod": { "startDate": "2026-11-01T00:00:00Z", "endDate": "2028-12-31T00:00:00Z" },
    "notifications": {
      "actual50": { "enabled": true, "operator": "GreaterThan", "threshold": 50, "contactEmails": ["<email>"], "thresholdType": "Actual" },
      "actual90": { "enabled": true, "operator": "GreaterThan", "threshold": 90, "contactEmails": ["<email>"], "thresholdType": "Actual" }
    }
  }
}
```

```bash
az rest --method put \
  --url "https://management.azure.com/subscriptions/<subscription-id>/providers/Microsoft.Consumption/budgets/senabayu-monthly?api-version=2023-05-01" \
  --body @budget.json
```

## 2. Resource providers

A new subscription has to opt in to each Azure service before it can use it. Free.

```bash
az provider register -n Microsoft.App --wait
az provider register -n Microsoft.DBforPostgreSQL --wait
az provider register -n Microsoft.OperationalInsights --wait
az extension add --name containerapp --upgrade
```

## 3. Resource group

The container for everything in this project. Deleting it deletes all of it.

```bash
az group create -n rg-senabayu -l malaysiawest
```

## 4. PostgreSQL Flexible Server

Secrets live in the git-ignored `.env.azure`. Load it first with `. ./.env.azure`.

```bash
MYIP=$(curl -4 -s https://checkip.amazonaws.com | tr -d '[:space:]')

az postgres flexible-server create \
  --resource-group rg-senabayu --name "$PG_SERVER" --location malaysiawest \
  --tier Burstable --sku-name Standard_B1ms --storage-size 32 --version 16 \
  --zonal-resiliency Disabled --backup-retention 7 \
  --admin-user "$PG_ADMIN_USER" --admin-password "$PG_ADMIN_PASSWORD" \
  --public-access "$MYIP" --yes -o none

az postgres flexible-server db create -g rg-senabayu -s "$PG_SERVER" -n "$PG_DB"

# 0.0.0.0 is Azure's special value for "services running in Azure". They still need the password.
az postgres flexible-server firewall-rule create -g rg-senabayu -s "$PG_SERVER" \
  -n allow-azure-services --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

az postgres flexible-server firewall-rule create -g rg-senabayu -s "$PG_SERVER" \
  -n allow-dev-machine --start-ip-address "$MYIP" --end-ip-address "$MYIP"
```

If your IP changes and connections time out, point `allow-dev-machine` at the new IP.

### App login

The API connects as `senabayu_app`, which can read and write rows but can't change the schema. Migrations use the
admin login.

```bash
docker run --rm -i -e PGPASSWORD="$PG_ADMIN_PASSWORD" -e PGSSLMODE=require postgres:16-alpine \
  psql -h "$PG_HOST" -U "$PG_ADMIN_USER" -d "$PG_DB" -v ON_ERROR_STOP=1 \
  -v app="$PG_APP_USER" -v app_pw="$PG_APP_PASSWORD" -v admin="$PG_ADMIN_USER" -v db="$PG_DB" <<'SQL'
CREATE ROLE :"app" LOGIN PASSWORD :'app_pw';
GRANT CONNECT ON DATABASE :"db" TO :"app";
GRANT USAGE ON SCHEMA public TO :"app";
ALTER DEFAULT PRIVILEGES FOR ROLE :"admin" IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app";
ALTER DEFAULT PRIVILEGES FOR ROLE :"admin" IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO :"app";
SQL
```

### Migrations and seed data

Run by hand from your machine, never on app startup. The bundle also runs `SeedData`.

```bash
dotnet ef migrations bundle --project Homestay.Api -o ./efbundle --force
./efbundle --connection "Host=$PG_HOST;Port=5432;Database=$PG_DB;Username=$PG_ADMIN_USER;Password=$PG_ADMIN_PASSWORD;SSL Mode=Require"
rm ./efbundle
```

## 5. Container image

Azure runs x86 (`amd64`), so build for that platform even on an ARM Mac.

```bash
gh auth refresh -h github.com -s write:packages   # once
gh auth token | docker login ghcr.io -u kimchi11y --password-stdin

SHA=$(git rev-parse HEAD)
docker buildx build --platform linux/amd64 -t ghcr.io/kimchi11y/senabayu-api:$SHA --load .
docker push ghcr.io/kimchi11y/senabayu-api:$SHA
```

## 6. Container Apps environment

Set the mode explicitly. Without `--environment-mode`, the CLI (2.92) creates an **Express** environment, which
rejects custom domains with `ExpressCustomDomainsNotSupported`.

```bash
az containerapp env create -g rg-senabayu -n senabayu-env-std -l malaysiawest --environment-mode WorkloadProfiles
```

## 7. Container App

The GHCR package must be public first: GitHub profile → Packages → `senabayu-api` → Package settings → Danger Zone.
GitHub has no API for this. The connection string is a Container Apps secret, using the app login, not the admin.

```bash
SHA=$(git rev-parse HEAD)
az containerapp create -g rg-senabayu -n senabayu-api --environment senabayu-env-std --workload-profile-name Consumption \
  --image ghcr.io/kimchi11y/senabayu-api:$SHA --target-port 8080 --ingress external \
  --min-replicas 0 --max-replicas 2 --cpu 0.25 --memory 0.5Gi \
  --secrets "pg-connection=Host=$PG_HOST;Port=5432;Database=$PG_DB;Username=$PG_APP_USER;Password=$PG_APP_PASSWORD;SSL Mode=Require;GSS Encryption Mode=Disable" \
  --env-vars "ConnectionStrings__Default=secretref:pg-connection"
```

Health probes have no CLI flags, so they are set from YAML. `probes.yaml` holds no secrets, only the secret's name:

```yaml
properties:
  template:
    containers:
      - name: senabayu-api
        image: ghcr.io/kimchi11y/senabayu-api:<sha>
        env:
          - name: ConnectionStrings__Default
            secretRef: pg-connection
        resources:
          cpu: 0.25
          memory: 0.5Gi
        probes:
          - type: Liveness
            httpGet: { path: /health/live, port: 8080 }
            periodSeconds: 30
          - type: Readiness
            httpGet: { path: /health/ready, port: 8080 }
            periodSeconds: 10
            failureThreshold: 3
          - type: Startup
            httpGet: { path: /health/live, port: 8080 }
            periodSeconds: 5
            failureThreshold: 24
```

```bash
az containerapp update -g rg-senabayu -n senabayu-api --yaml probes.yaml
az containerapp logs show -g rg-senabayu -n senabayu-api --tail 50   # where the logs are
```

## 8. Custom domain

In Cloudflare DNS, with the CNAME set to **DNS only** (grey cloud) so Azure can issue and renew the certificate:

| Type | Name | Content |
|---|---|---|
| `CNAME` | `api.senabayu-demo` | the app's `*.azurecontainerapps.io` FQDN |
| `TXT` | `asuid.api.senabayu-demo` | `az containerapp show -g rg-senabayu -n senabayu-api --query properties.customDomainVerificationId -o tsv` |

```bash
D=api.senabayu-demo.hakimrazak.com
az containerapp hostname add -g rg-senabayu -n senabayu-api --hostname $D
az containerapp hostname bind -g rg-senabayu -n senabayu-api --hostname $D \
  --environment senabayu-env-std --validation-method CNAME
```

## 9. API signing key and migration job

Values come from `.env.azure`. The job runs the migrator image with the admin login, which only Azure holds. The image
here is a placeholder, because every deploy replaces it. Setting `Admin__Email` and `Admin__Password` creates the admin
login the first time the job runs, if none exists yet.

```bash
az containerapp secret set -g rg-senabayu -n senabayu-api --secrets "jwt-signing-key=$JWT_SIGNING_KEY"
az containerapp update -g rg-senabayu -n senabayu-api --set-env-vars "Jwt__SigningKey=secretref:jwt-signing-key"

az containerapp job create -g rg-senabayu -n senabayu-migrate --environment senabayu-env-std \
  --workload-profile-name Consumption --trigger-type Manual \
  --replica-timeout 600 --replica-retry-limit 0 --parallelism 1 --replica-completion-count 1 \
  --image mcr.microsoft.com/dotnet/aspnet:10.0 --cpu 0.25 --memory 0.5Gi \
  --secrets "pg-admin-connection=Host=$PG_HOST;Port=5432;Database=$PG_DB;Username=$PG_ADMIN_USER;Password=$PG_ADMIN_PASSWORD;SSL Mode=Require;GSS Encryption Mode=Disable" \
            "admin-password=$ADMIN_PASSWORD" \
  --env-vars "ConnectionStrings__Default=secretref:pg-admin-connection" \
             "Admin__Email=$ADMIN_EMAIL" "Admin__Password=secretref:admin-password"
```

## 10. Deploys from GitHub Actions (OIDC)

GitHub tokens name the repo with its numeric IDs (`gh api repos/kimchi11y/senabayu-api --jq "[.owner.id, .id]"`).

A user-assigned managed identity that only `main` of this repo can log in as, limited to Container Apps in this
resource group and the migration job. There is no stored secret: the three values below are identifiers, so they are GitHub **variables**.

```bash
az provider register -n Microsoft.ManagedIdentity --wait
az identity create -g rg-senabayu -n senabayu-github-deploy -l malaysiawest

az identity federated-credential create -g rg-senabayu --identity-name senabayu-github-deploy -n github-main \
  --issuer https://token.actions.githubusercontent.com \
  --subject "repo:kimchi11y@<owner-id>/senabayu-api@<repo-id>:ref:refs/heads/main" \
  --audiences api://AzureADTokenExchange

az role assignment create \
  --assignee-object-id "$(az identity show -g rg-senabayu -n senabayu-github-deploy --query principalId -o tsv)" \
  --assignee-principal-type ServicePrincipal --role "Container Apps Contributor" \
  --scope "$(az group show -n rg-senabayu --query id -o tsv)"

# Without this role, job commands fail with a misleading "job does not exist".
az role assignment create \
  --assignee-object-id "$(az identity show -g rg-senabayu -n senabayu-github-deploy --query principalId -o tsv)" \
  --assignee-principal-type ServicePrincipal --role "Container Apps Jobs Contributor" \
  --scope "$(az containerapp job show -g rg-senabayu -n senabayu-migrate --query id -o tsv)"

gh variable set AZURE_CLIENT_ID --body "$(az identity show -g rg-senabayu -n senabayu-github-deploy --query clientId -o tsv)"
gh variable set AZURE_TENANT_ID --body "$(az account show --query tenantId -o tsv)"
gh variable set AZURE_SUBSCRIPTION_ID --body "$(az account show --query id -o tsv)"
```

The GHCR package was first pushed by hand, so the workflow's `GITHUB_TOKEN` needs access: package settings → Manage
Actions access → Add Repository → `senabayu-api` → role **Write**.

To roll back, point the app at an earlier commit's image:

```bash
az containerapp update -g rg-senabayu -n senabayu-api --image ghcr.io/kimchi11y/senabayu-api:<earlier-sha>
```
