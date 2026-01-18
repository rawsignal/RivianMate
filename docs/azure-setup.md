# Azure Deployment Setup

This guide covers deploying RivianMate Pro to Azure Container Apps.

## Prerequisites

- Azure CLI installed
- Azure subscription
- GitHub repository with Actions enabled

## Azure Resources

You'll need to create:

1. **Resource Group**
2. **Azure Container Registry (ACR)**
3. **Azure Container Apps Environment**
4. **Azure Container App**
5. **Azure Database for PostgreSQL Flexible Server**

## Step 1: Create Resource Group

```bash
az group create --name rivianmate-prod --location eastus
```

## Step 2: Create Container Registry

```bash
az acr create \
  --resource-group rivianmate-prod \
  --name rivianmateprod \
  --sku Basic \
  --admin-enabled true
```

## Step 3: Create PostgreSQL Database

```bash
az postgres flexible-server create \
  --resource-group rivianmate-prod \
  --name rivianmate-db \
  --location eastus \
  --admin-user rivianmate \
  --admin-password <STRONG_PASSWORD> \
  --sku-name Standard_B1ms \
  --tier Burstable \
  --storage-size 32 \
  --version 15

# Create the database
az postgres flexible-server db create \
  --resource-group rivianmate-prod \
  --server-name rivianmate-db \
  --database-name rivianmate
```

## Step 4: Create Container Apps Environment

```bash
az containerapp env create \
  --name rivianmate-env \
  --resource-group rivianmate-prod \
  --location eastus
```

## Step 5: Create Container App

```bash
az containerapp create \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --environment rivianmate-env \
  --image rivianmateprod.azurecr.io/rivianmate:latest \
  --registry-server rivianmateprod.azurecr.io \
  --target-port 8080 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 3 \
  --cpu 0.5 \
  --memory 1Gi \
  --env-vars \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ConnectionStrings__DefaultConnection=Host=rivianmate-db.postgres.database.azure.com;Database=rivianmate;Username=rivianmate;Password=<PASSWORD>" \
    "RivianMate__AdminEmails__0=your@email.com"
```

## Step 6: Configure GitHub Secrets

Add these secrets to your GitHub repository:

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Service principal JSON (see below) |
| `AZURE_CONTAINER_REGISTRY` | ACR name (e.g., `rivianmateprod`) |

### Create Service Principal

```bash
az ad sp create-for-rbac \
  --name "rivianmate-github" \
  --role contributor \
  --scopes /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/rivianmate-prod \
  --sdk-auth
```

Copy the JSON output to `AZURE_CREDENTIALS` secret.

### Grant ACR Access

```bash
az role assignment create \
  --assignee <SERVICE_PRINCIPAL_APP_ID> \
  --scope /subscriptions/<SUBSCRIPTION_ID>/resourceGroups/rivianmate-prod/providers/Microsoft.ContainerRegistry/registries/rivianmateprod \
  --role AcrPush
```

## Step 7: Configure Custom Domain (Optional)

```bash
az containerapp hostname add \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --hostname app.rivianmate.com

# Add managed certificate
az containerapp hostname bind \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --hostname app.rivianmate.com \
  --environment rivianmate-env \
  --validation-method CNAME
```

## Environment Variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | PostgreSQL connection string |
| `RivianMate__AdminEmails__0` | Admin email for Hangfire access |
| `RivianMate__Polling__Enabled` | Enable/disable vehicle polling |

## Estimated Costs

- **Container Apps**: ~$15-30/month (depends on usage)
- **PostgreSQL Flexible Server (B1ms)**: ~$15/month
- **Container Registry (Basic)**: ~$5/month

Total: ~$35-50/month for a small deployment

## Monitoring

Azure Container Apps integrates with Azure Monitor. View logs:

```bash
az containerapp logs show \
  --name rivianmate-pro \
  --resource-group rivianmate-prod \
  --follow
```
