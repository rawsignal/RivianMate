# Azure Deployment Setup

This guide covers deploying RivianMate Pro to Azure Container Apps.

## Prerequisites

- Azure CLI installed (`az --version` to verify)
- Azure subscription
- Visual Studio closed (required for Docker builds to avoid file locks)

## Azure Resources Overview

| Resource | Name | Purpose |
|----------|------|---------|
| Resource Group | rivianmate-prod | Container for all resources |
| Container Registry | rivianmateprod | Stores Docker images |
| PostgreSQL Flexible Server | rivianmate-db | Database |
| Container Apps Environment | rivianmate-env | Container hosting environment |
| Container App | rivianmate-pro | The application |

## Step 1: Login to Azure

```bash
# If you have MFA, use device code flow
az login --use-device-code
```

## Step 2: Create Resource Group

```bash
az group create --name rivianmate-prod --location centralus
```

## Step 3: Register Required Providers

If you get "MissingSubscriptionRegistration" errors, register these providers:

```bash
az provider register --namespace Microsoft.ContainerRegistry
az provider register --namespace Microsoft.App
az provider register --namespace Microsoft.DBforPostgreSQL
az provider register --namespace Microsoft.OperationalInsights
```

Wait for registration to complete (can take a few minutes).

## Step 4: Create Container Registry

```bash
az acr create \
  --resource-group rivianmate-prod \
  --name rivianmateprod \
  --sku Basic \
  --admin-enabled true
```

## Step 5: Create PostgreSQL Database

```bash
# Create the server
az postgres flexible-server create \
  --resource-group rivianmate-prod \
  --name rivianmate-db \
  --location centralus \
  --admin-user rivianmate \
  --admin-password <STRONG_PASSWORD> \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --version 16 \
  --yes

# Create the database
az postgres flexible-server db create \
  --resource-group rivianmate-prod \
  --server-name rivianmate-db \
  --database-name rivianmate
```

## Step 6: Create Container Apps Environment

```bash
az containerapp env create \
  --name rivianmate-env \
  --resource-group rivianmate-prod \
  --location centralus
```

## Step 7: Build and Push Docker Image

**Important:** Close Visual Studio before running this command to avoid file lock errors.

```bash
# From the repository root directory
az acr build \
  --registry rivianmateprod \
  --image rivianmate:v1 \
  --build-arg EDITION=Pro \
  --file Dockerfile .
```

The `--build-arg EDITION=Pro` flag builds the Pro edition with all features enabled.

## Step 8: Create Container App

```bash
az containerapp create \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --environment rivianmate-env \
  --image rivianmateprod.azurecr.io/rivianmate:v1 \
  --target-port 8080 \
  --ingress external \
  --registry-server rivianmateprod.azurecr.io \
  --env-vars \
    "ConnectionStrings__DefaultConnection=Host=rivianmate-db.postgres.database.azure.com;Database=rivianmate;Username=rivianmate;Password=<PASSWORD>;SSL Mode=Require" \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "RivianMate__AdminEmails__0=your@email.com"
```

Your app will be available at: `https://rivianmate-pro.<random>.centralus.azurecontainerapps.io`

## Deploying Updates

When deploying updates, use a new version tag to force Azure to pull the new image:

```bash
# Build with new version
az acr build \
  --registry rivianmateprod \
  --image rivianmate:v2 \
  --build-arg EDITION=Pro \
  --file Dockerfile .

# Update the container app
az containerapp update \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --image rivianmateprod.azurecr.io/rivianmate:v2
```

Increment the version (v2, v3, etc.) for each deployment. Using `:latest` may not trigger updates due to caching.

**Note:** Blazor Server uses persistent SignalR connections. Existing browser sessions will stay on the old revision until refreshed. Do a hard refresh (Ctrl+Shift+R) to connect to the new revision.

## Troubleshooting

### File Lock Errors During Build

```
[Errno 13] Permission denied: '.\src\.vs\...'
```

**Solution:** Close Visual Studio before running `az acr build`.

### Image Not Found After Build

```
MANIFEST_UNKNOWN: manifest tagged by "latest" is not found
```

**Solution:** Use explicit version tags instead of `:latest`. Check what images exist:

```bash
az acr repository show-tags --name rivianmateprod --repository rivianmate
```

### Database Migration Errors

If you see errors like `relation "X" already exists` or type casting errors:

```bash
# Drop and recreate the database (only for fresh deployments!)
az postgres flexible-server db delete \
  --resource-group rivianmate-prod \
  --server-name rivianmate-db \
  --database-name rivianmate \
  --yes

az postgres flexible-server db create \
  --resource-group rivianmate-prod \
  --server-name rivianmate-db \
  --database-name rivianmate
```

### Container Keeps Crashing

Check the logs:

```bash
az containerapp logs show \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --follow
```

### Revision Not Updating

Check current revisions:

```bash
az containerapp revision list \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  -o table
```

Force a new revision:

```bash
az containerapp update \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --image rivianmateprod.azurecr.io/rivianmate:v3 \
  --revision-suffix v3
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string with `SSL Mode=Require` |
| `ASPNETCORE_ENVIRONMENT` | Set to `Production` |
| `RivianMate__AdminEmails__0` | Email address for Hangfire dashboard access |
| `RivianMate__Polling__Enabled` | Enable/disable vehicle polling (default: true) |

## Estimated Costs

| Resource | Monthly Cost |
|----------|-------------|
| Container Apps (1 replica) | ~$15-30 |
| PostgreSQL Flexible Server (B1ms) | ~$15 |
| Container Registry (Basic) | ~$5 |
| **Total** | **~$35-50/month** |

## GitHub Actions (CI/CD)

See `.github/workflows/deploy-pro.yml` for automated deployments. Required secrets:

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Service principal JSON |
| `AZURE_CONTAINER_REGISTRY` | ACR name (e.g., `rivianmateprod`) |

### Create Service Principal

```bash
az ad sp create-for-rbac \
  --name "rivianmate-github" \
  --role contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/rivianmate-prod \
  --sdk-auth
```

Copy the JSON output to the `AZURE_CREDENTIALS` secret.
