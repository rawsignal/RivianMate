# Changelog

## 2026-01-18 - Azure Deployment & Bug Fixes

### Infrastructure

#### Database Migrations
- Deleted all existing migrations that had mixed SQLite/PostgreSQL types
- Created `DesignTimeDbContextFactory` to ensure migrations are always generated for PostgreSQL
- Regenerated fresh `InitialCreate` migration with proper PostgreSQL types

#### Azure Container Apps Deployment
- Deployed Pro edition to Azure Container Apps
- Resources: rivianmate-prod (resource group), rivianmateprod (ACR), rivianmate-db (PostgreSQL), rivianmate-env (Container Apps environment)
- URL: `https://rivianmate-pro.<domain>.centralus.azurecontainerapps.io`

### Bug Fixes

#### Dashboard Drag and Drop Not Completing
**Problem:** When dragging dashboard cards, the card would pick up and show the placeholder, but never complete the drop. The placeholder shadow would remain stuck.

**Root Cause:** If `CommitReorder()` threw an exception during the database operation, the drag state variables (`_draggingCardId`, `_hoverTargetCardId`) were never cleared.

**Fix:** Added try/catch/finally block in `Home.razor` to always clear drag state and call `StateHasChanged()`:
```csharp
try {
    // reorder logic
} catch (Exception ex) {
    Logger.LogError(ex, "Failed to reorder cards");
} finally {
    _draggingCardId = null;
    _hoverTargetCardId = null;
    StateHasChanged();
}
```

#### DbContext Disposed in Blazor Server
**Problem:** `ObjectDisposedException: Cannot access a disposed context instance` errors when using the dashboard config service.

**Root Cause:** Blazor Server components live longer than the scoped DbContext. When async operations await, the scope can end and dispose the context.

**Fix:**
1. Changed `Program.cs` to use `AddDbContextFactory` instead of `AddDbContext`
2. Updated `DashboardConfigService` to inject `IDbContextFactory<RivianMateDbContext>` and create short-lived contexts per operation:
```csharp
public async Task<List<DashboardCardConfig>> GetUserConfigAsync(Guid userId)
{
    await using var db = await _dbFactory.CreateDbContextAsync();
    // ... use db
}
```

### Features

#### Edition Display in Navbar
- Navbar now displays "RivianMate Pro" for Pro builds, "RivianMate" for Self-Hosted
- Uses `BuildInfo.DisplayName` which is set at compile time

### Code Changes Summary

| File | Change |
|------|--------|
| `Program.cs` | Changed `AddDbContext` to `AddDbContextFactory`, added scoped DbContext registration |
| `DashboardConfigService.cs` | Changed to use `IDbContextFactory`, each method creates its own context |
| `Home.razor` | Added try/finally to `CommitReorder()` for reliable state cleanup |
| `MainLayout.razor` | Added `BuildInfo.DisplayName` to show edition in navbar |
| `DesignTimeDbContextFactory.cs` | New file - ensures migrations generate for PostgreSQL |
| `Migrations/*` | Regenerated fresh InitialCreate for PostgreSQL |

### Documentation Updates

| File | Change |
|------|--------|
| `docs/azure-setup.md` | Complete rewrite with actual deployment steps and troubleshooting |
| `docs/LICENSING.md` | Updated feature table to match actual code, added technical implementation section |
| `docs/CLOUD_DEPLOYMENT.md` | Removed old RM_DK runtime key references, updated to compile-time edition |
