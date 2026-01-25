# RivianMate Development Guide

## Edition System

RivianMate has two editions with compile-time differences:

- **SelfHosted** (default): For self-hosted users, uses obfuscated table names
- **Pro**: For production cloud deployment, uses standard table names

### Building Different Editions

```bash
# SelfHosted (default)
dotnet build

# Pro edition
dotnet build -p:Edition=Pro

# Docker
docker build --build-arg EDITION=SelfHosted -t rivianmate:selfhosted .
docker build --build-arg EDITION=Pro -t rivianmate:pro .
```

## Database Table Names

Table names differ between editions to prevent self-hosted users from knowing production table structure. The mapping is defined in `src/RivianMate.Infrastructure/Data/TableNames.cs`.

| Entity | Pro | SelfHosted |
|--------|-----|------------|
| ApplicationUser | AspNetUsers | Users |
| Vehicle | Vehicles | Cars |
| Drive | Drives | Trips |
| ChargingSession | ChargingSessions | ChargeRecords |
| ... | (standard names) | (obfuscated names) |

**Important**: When writing raw SQL or creating manual migrations, always use `TableNames.*` constants, never hardcoded strings.

## Database Migrations

### Automatic Migration on Startup

Migrations run automatically when the application starts:

- **PostgreSQL**: Uses EF Core `MigrateAsync()` which applies pending migrations and is idempotent (safe to run multiple times)
- **SQLite**: Uses `EnsureCreatedAsync()` for development, with manual schema updates for new columns/tables

The startup code includes retry logic for Docker environments where the database may not be immediately available.

### Creating New Migrations

Since table names differ between editions, migrations must be generated for each edition:

```bash
# Generate migration for SelfHosted edition
dotnet ef migrations add MigrationName -p src/RivianMate.Infrastructure -s src/RivianMate.Api

# Generate migration for Pro edition
dotnet ef migrations add MigrationName -p src/RivianMate.Infrastructure -s src/RivianMate.Api -- --Edition Pro
```

**Best Practice**: Keep migrations simple. Complex data transformations should be handled in application code during startup if needed.

### Migration Safety

The startup migration code handles:
- Database not yet available (retries with exponential backoff)
- Migrations already applied (no-op, no error)
- New migrations needed (applies them automatically)

Users never need to run migrations manually - Docker startup handles everything.

## Adding New Entities

When adding a new database entity:

1. Create the entity class in `src/RivianMate.Core/Entities/`
2. Add `DbSet<T>` property in `RivianMateDbContext`
3. Add table names to `TableNames.cs` (both Pro and SelfHosted sections)
4. Add `ToTable()` call in `ConfigureTableNames()` method
5. Add entity configuration in `OnModelCreating()`
6. Generate migrations for both editions

Example for a new `Notification` entity:

```csharp
// In TableNames.cs
#if EDITION_PRO
    public const string Notifications = "Notifications";
#else
    public const string Notifications = "Alerts";
#endif

// In RivianMateDbContext.cs ConfigureTableNames()
modelBuilder.Entity<Notification>().ToTable(TableNames.Notifications);
```

## SQLite Development Mode

For local development with SQLite (`DatabaseProvider=Sqlite`):

- Schema is created from the model using `EnsureCreatedAsync()`
- New columns are added via `ALTER TABLE` if missing
- New tables are created with `CREATE TABLE IF NOT EXISTS` pattern
- Always use `Tables.*` constants (alias for `TableNames`) in Program.cs

## Docker Deployment

Both editions use the same Dockerfile with the `EDITION` build argument:

```yaml
# docker-compose.yml
services:
  rivianmate:
    build:
      context: .
      args:
        EDITION: SelfHosted  # or Pro
```

The application handles:
- Waiting for PostgreSQL to be ready
- Applying migrations automatically
- No manual intervention required
