# Configuration Reference

Complete reference for all RivianMate configuration options.

## Configuration Sources

RivianMate reads configuration from multiple sources (in order of priority):

1. **Environment variables** (highest priority)
2. **appsettings.{Environment}.json**
3. **appsettings.json** (lowest priority)

Environment variables override JSON settings. Use `__` (double underscore) to represent nested JSON keys.

---

## Environment Variables

### Database Connection

| Variable | Description | Required | Default |
|----------|-------------|----------|---------|
| `DATABASE_URL` | Full PostgreSQL connection string | No* | - |
| `POSTGRES_HOST` | PostgreSQL server hostname | No* | `localhost` |
| `POSTGRES_PORT` | PostgreSQL server port | No | `5432` |
| `POSTGRES_DB` | Database name | No | `rivianmate` |
| `POSTGRES_USER` | Database username | No | `rivianmate` |
| `POSTGRES_PASSWORD` | Database password | No | `rivianmate` |
| `DatabaseProvider` | `Sqlite` for dev, omit for PostgreSQL | No | PostgreSQL |

*Either `DATABASE_URL` or `POSTGRES_HOST` is required for production.

**Connection string priority:**
1. `DATABASE_URL` (used as-is if set)
2. Composed from `POSTGRES_*` variables
3. `ConnectionStrings__DefaultConnection`
4. Default from appsettings.json

**Examples:**

```bash
# Full connection string
DATABASE_URL="Host=db.example.com;Port=5432;Database=rivianmate;Username=user;Password=pass"

# Individual components
POSTGRES_HOST=db.example.com
POSTGRES_PORT=5432
POSTGRES_DB=rivianmate
POSTGRES_USER=rivianmate
POSTGRES_PASSWORD=secretpassword

# Azure PostgreSQL (with SSL)
DATABASE_URL="Host=server.postgres.database.azure.com;Database=rivianmate;Username=user;Password=pass;SSL Mode=Require"

# SQLite for development
DatabaseProvider=Sqlite
ConnectionStrings__DefaultConnection="Data Source=rivianmate.db"
```

---

### ASP.NET Core

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` |
| `ASPNETCORE_URLS` | HTTP listen URLs | `http://+:8080` |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | Trust forwarded headers | `false` |

**Examples:**

```bash
# Production settings
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS="http://+:8080"

# Development settings
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS="http://localhost:5000;https://localhost:5001"

# Behind reverse proxy
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
```

---

### Logging

| Variable | Description | Default |
|----------|-------------|---------|
| `Logging__LogLevel__Default` | Default log level | `Information` |
| `Logging__LogLevel__Microsoft.AspNetCore` | ASP.NET Core logging | `Warning` |
| `Logging__LogLevel__Microsoft.EntityFrameworkCore` | EF Core logging | `Warning` |
| `Logging__LogLevel__RivianMate` | Application logging | `Information` |

**Log levels:** `Trace`, `Debug`, `Information`, `Warning`, `Error`, `Critical`, `None`

```bash
# Verbose logging for debugging
Logging__LogLevel__Default=Debug
Logging__LogLevel__RivianMate=Debug
Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command=Information

# Minimal logging for production
Logging__LogLevel__Default=Warning
```

---

### Polling Configuration

| Variable | Description | Default |
|----------|-------------|---------|
| `RivianMate__Polling__IntervalAwakeSeconds` | Polling interval when vehicle is awake | `30` |
| `RivianMate__Polling__IntervalAsleepSeconds` | Polling interval when vehicle is asleep | `300` |
| `RivianMate__Polling__Enabled` | Enable automatic polling | `true` |

```bash
# Aggressive polling (more API calls)
RivianMate__Polling__IntervalAwakeSeconds=15
RivianMate__Polling__IntervalAsleepSeconds=120

# Conservative polling (fewer API calls)
RivianMate__Polling__IntervalAwakeSeconds=60
RivianMate__Polling__IntervalAsleepSeconds=600

# Disable polling entirely
RivianMate__Polling__Enabled=false
```

---

### Data Retention

| Variable | Description | Default |
|----------|-------------|---------|
| `RivianMate__DataRetention__RawStateDays` | Days to keep raw state data | `90` |
| `RivianMate__DataRetention__PositionDataDays` | Days to keep position/location data | `365` |

```bash
# Keep more history
RivianMate__DataRetention__RawStateDays=180
RivianMate__DataRetention__PositionDataDays=730

# Minimal storage
RivianMate__DataRetention__RawStateDays=30
RivianMate__DataRetention__PositionDataDays=90
```

---

## appsettings.json Reference

Complete configuration file structure:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "RivianMate": "Information"
    }
  },

  "AllowedHosts": "*",

  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=rivianmate;Username=rivianmate;Password=rivianmate"
  },

  "DatabaseProvider": null,

  "License": {
    "IsCloudHosted": false
  },

  "RivianMate": {
    "Polling": {
      "IntervalAwakeSeconds": 30,
      "IntervalAsleepSeconds": 300,
      "Enabled": true
    },
    "DataRetention": {
      "RawStateDays": 90,
      "PositionDataDays": 365
    }
  }
}
```

---

## appsettings.Development.json

Development-specific overrides:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore.Database.Command": "Information",
      "RivianMate": "Debug"
    }
  },
  "DetailedErrors": true,
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=rivianmate.db"
  },
  "DatabaseProvider": "Sqlite"
}
```

---

## Docker Compose Environment

Example `docker-compose.yml` with all common options:

```yaml
version: "3.8"

services:
  rivianmate:
    image: ghcr.io/yourusername/rivianmate:latest
    container_name: rivianmate
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      # Database
      - POSTGRES_HOST=db
      - POSTGRES_PORT=5432
      - POSTGRES_DB=rivianmate
      - POSTGRES_USER=rivianmate
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}

      # ASP.NET Core
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:8080

      # Logging
      - Logging__LogLevel__Default=Information

      # Polling
      - RivianMate__Polling__IntervalAwakeSeconds=30
      - RivianMate__Polling__IntervalAsleepSeconds=300

      # Data Retention
      - RivianMate__DataRetention__RawStateDays=90
    depends_on:
      db:
        condition: service_healthy

  db:
    image: postgres:16-alpine
    container_name: rivianmate-db
    restart: unless-stopped
    environment:
      - POSTGRES_DB=rivianmate
      - POSTGRES_USER=rivianmate
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD}
    volumes:
      - rivianmate-db:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U rivianmate -d rivianmate"]
      interval: 10s
      timeout: 5s
      retries: 5

volumes:
  rivianmate-db:
```

Use with a `.env` file:

```bash
# .env
POSTGRES_PASSWORD=your-secure-password-here
```

---

## In-App Settings

These settings are stored in the database and managed via the Settings UI:

| Setting | Description | Default |
|---------|-------------|---------|
| Polling Interval (Awake) | How often to poll when vehicle is active | 30 seconds |
| Polling Interval (Asleep) | How often to poll when vehicle is idle | 5 minutes |
| Distance Units | `miles` or `km` | `miles` |
| Temperature Units | `f` or `c` | `f` |
| Energy Units | `kwh` or `wh` | `kwh` |
| Home Location | Lat/Long for "home charging" detection | Not set |
| Battery Health Calculation Days | Days of data to use for health % | 30 |

---

## Database Settings Table

Settings stored in the `Settings` table:

| Key | Description | Encrypted |
|-----|-------------|-----------|
| `rivian:access_token` | Rivian API access token | Yes |
| `rivian:refresh_token` | Rivian API refresh token | Yes |
| `rivian:user_session_token` | Rivian session token | Yes |
| `rivian:csrf_token` | Rivian CSRF token | Yes |
| `rivian:app_session_token` | Rivian app session token | Yes |
| `rivian:token_expires_at` | Token expiration time | No |
| `rivian:user_id` | Rivian user ID | No |
| `polling:interval_awake_seconds` | Awake polling interval | No |
| `polling:interval_asleep_seconds` | Asleep polling interval | No |
| `polling:enabled` | Is polling enabled | No |
| `units:distance` | Distance unit preference | No |
| `units:temperature` | Temperature unit preference | No |
| `units:energy` | Energy unit preference | No |
| `home:latitude` | Home location latitude | No |
| `home:longitude` | Home location longitude | No |
| `home:radius_meters` | Home geofence radius | No |
| `battery_health:calculation_days` | Days for health calculation | No |
| `retention:raw_state_days` | Raw state data retention | No |
| `retention:position_days` | Position data retention | No |
| `license:key` | Enterprise license key | Yes |
| `license:instance_id` | Unique installation ID | No |

---

## Hangfire Configuration

Hangfire runs background jobs and is configured automatically:

| Aspect | Configuration |
|--------|---------------|
| Storage | PostgreSQL (same as app database) |
| Worker Count | `Environment.ProcessorCount * 2` |
| Queues | `default`, `polling` |
| Dashboard URL | `/hangfire` |
| Dashboard Auth | Authenticated users (admin in production) |

To access the Hangfire dashboard:
- Development: Open `/hangfire` (no auth required)
- Production: Must be authenticated user

---

## Identity Configuration

User authentication settings (hardcoded, not configurable):

| Setting | Value |
|---------|-------|
| Password Min Length | 12 characters |
| Require Digit | Yes |
| Require Lowercase | Yes |
| Require Uppercase | Yes |
| Require Non-Alphanumeric | Yes |
| Required Unique Characters | 4 |
| Lockout Duration | 15 minutes |
| Max Failed Attempts | 5 |
| Cookie Expiration | 30 days |
| Sliding Expiration | Yes |

---

## Troubleshooting Configuration

### Check Active Configuration

Add to your startup to log configuration:

```bash
# Set environment variable
Logging__LogLevel__Microsoft.Hosting.Lifetime=Information
```

### Validate Database Connection

```bash
# Using psql
psql "host=localhost dbname=rivianmate user=rivianmate password=yourpass" -c "SELECT 1"

# Using Docker
docker exec rivianmate-db psql -U rivianmate -d rivianmate -c "SELECT 1"
```

### Test Container Environment

```bash
# Print environment variables
docker exec rivianmate printenv | grep -E "(POSTGRES|ASPNET|Rivian)"

# Check logs
docker logs rivianmate --tail 100
```

### Common Issues

**"Could not connect to database"**
- Check `POSTGRES_HOST` is reachable from the container
- Verify credentials match between app and database
- Ensure database container is healthy

**"Connection refused on port 5432"**
- PostgreSQL might not be ready yet
- Check firewall/security group rules
- Verify port mapping in Docker

**"DataProtection key not found"**
- Keys are stored in database, needs successful DB connection first
- Check database migrations ran successfully
