# Self-Hosting RivianMate

This guide covers how to self-host RivianMate on your own infrastructure. Self-hosted installations run the **Community Edition** by default, which includes core features for personal use.

## Quick Start (Docker Compose)

The fastest way to get RivianMate running is with Docker Compose.

### Prerequisites

- Docker and Docker Compose installed
- At least 1GB RAM available
- 10GB disk space for database

### Steps

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/rivianmate.git
   cd rivianmate
   ```

2. **Start the stack**
   ```bash
   docker-compose up -d
   ```

3. **Access RivianMate**

   Open http://localhost:8080 in your browser.

4. **Create your account**

   Register a new account and link your Rivian credentials.

That's it! RivianMate will start polling your vehicle data automatically.

---

## Deployment Options

### Option 1: Docker Compose (Recommended)

The included `docker-compose.yml` runs RivianMate with PostgreSQL.

```yaml
services:
  rivianmate:
    build: .
    container_name: rivianmate
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      - POSTGRES_HOST=db
      - POSTGRES_PORT=5432
      - POSTGRES_DB=rivianmate
      - POSTGRES_USER=rivianmate
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD:-rivianmate}
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
      - POSTGRES_PASSWORD=${POSTGRES_PASSWORD:-rivianmate}
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

**Production recommendations:**
- Change `POSTGRES_PASSWORD` to a strong password
- Use a `.env` file for secrets
- Consider adding a reverse proxy (nginx/traefik) for HTTPS

### Option 2: Unraid

RivianMate works great on Unraid via Community Applications.

1. **Add the template** (or install from Community Apps when available)

2. **Configure the container:**

   | Parameter | Value |
   |-----------|-------|
   | Repository | `ghcr.io/yourusername/rivianmate:latest` |
   | Network Type | Bridge |
   | Port | 8080 → 8080 |
   | POSTGRES_HOST | Your PostgreSQL IP |
   | POSTGRES_PORT | 5432 |
   | POSTGRES_DB | rivianmate |
   | POSTGRES_USER | rivianmate |
   | POSTGRES_PASSWORD | (your password) |

3. **Database options:**
   - Use the built-in PostgreSQL container from Community Apps
   - Or use an external PostgreSQL server

### Option 3: Synology NAS

1. **Install Docker** from Package Center

2. **Download the image:**
   ```bash
   docker pull ghcr.io/yourusername/rivianmate:latest
   ```

3. **Create via Docker UI:**
   - Container → Create
   - Select the rivianmate image
   - Configure port 8080
   - Add environment variables for PostgreSQL

4. **PostgreSQL:**
   - Install PostgreSQL from Package Center, or
   - Run PostgreSQL in a separate container

### Option 4: Manual Installation

For running without Docker:

1. **Prerequisites:**
   - .NET 8 Runtime
   - PostgreSQL 14+

2. **Download release:**
   ```bash
   wget https://github.com/yourusername/rivianmate/releases/latest/download/rivianmate-linux-x64.tar.gz
   tar -xzf rivianmate-linux-x64.tar.gz
   cd rivianmate
   ```

3. **Configure:**
   ```bash
   export DATABASE_URL="Host=localhost;Database=rivianmate;Username=rivianmate;Password=yourpassword"
   export ASPNETCORE_URLS="http://+:8080"
   ```

4. **Run:**
   ```bash
   ./RivianMate.Api
   ```

5. **Systemd service (optional):**
   ```ini
   # /etc/systemd/system/rivianmate.service
   [Unit]
   Description=RivianMate
   After=network.target postgresql.service

   [Service]
   Type=notify
   User=rivianmate
   WorkingDirectory=/opt/rivianmate
   ExecStart=/opt/rivianmate/RivianMate.Api
   Restart=always
   Environment=DATABASE_URL=Host=localhost;Database=rivianmate;Username=rivianmate;Password=yourpassword
   Environment=ASPNETCORE_URLS=http://+:8080

   [Install]
   WantedBy=multi-user.target
   ```

---

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_URL` | Full PostgreSQL connection string | - |
| `POSTGRES_HOST` | PostgreSQL hostname | localhost |
| `POSTGRES_PORT` | PostgreSQL port | 5432 |
| `POSTGRES_DB` | Database name | rivianmate |
| `POSTGRES_USER` | Database username | rivianmate |
| `POSTGRES_PASSWORD` | Database password | rivianmate |
| `ASPNETCORE_URLS` | Listen URL | http://+:8080 |
| `ASPNETCORE_ENVIRONMENT` | Environment | Production |

Database connection priority:
1. `DATABASE_URL` (if set, used directly)
2. `POSTGRES_*` environment variables (composed into connection string)
3. `ConnectionStrings__DefaultConnection` appsettings

### Polling Configuration

Configure via the Settings page in the UI, or set defaults in appsettings:

```json
{
  "RivianMate": {
    "Polling": {
      "IntervalAwakeSeconds": 30,
      "IntervalAsleepSeconds": 300,
      "Enabled": true
    }
  }
}
```

### Email Configuration

RivianMate supports sending emails for password resets, security alerts, and admin broadcasts. For self-hosted installations, you can use SMTP with any email provider (Gmail, Mailgun, SendGrid, your own mail server, etc.).

#### Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `RivianMate__Email__Enabled` | Enable/disable email system | `true` |
| `RivianMate__Email__Provider` | Email provider (`SMTP` or `Resend`) | `SMTP` |
| `RivianMate__Email__FromAddress` | Sender email address | `noreply@yourdomain.com` |
| `RivianMate__Email__FromName` | Sender display name | `RivianMate` |
| `RivianMate__Email__BaseUrl` | Your RivianMate URL (for email links) | `https://rivianmate.yourdomain.com` |
| `RivianMate__Email__SMTP__Host` | SMTP server hostname | `smtp.gmail.com` |
| `RivianMate__Email__SMTP__Port` | SMTP server port | `587` |
| `RivianMate__Email__SMTP__Username` | SMTP username | `your@gmail.com` |
| `RivianMate__Email__SMTP__Password` | SMTP password or app password | `your-app-password` |
| `RivianMate__Email__SMTP__UseSsl` | Use TLS/SSL | `true` |

#### Docker Compose Example

```yaml
services:
  rivianmate:
    environment:
      - RivianMate__Email__Enabled=true
      - RivianMate__Email__Provider=SMTP
      - RivianMate__Email__FromAddress=noreply@yourdomain.com
      - RivianMate__Email__FromName=RivianMate
      - RivianMate__Email__BaseUrl=https://rivianmate.yourdomain.com
      - RivianMate__Email__SMTP__Host=smtp.gmail.com
      - RivianMate__Email__SMTP__Port=587
      - RivianMate__Email__SMTP__Username=${SMTP_USERNAME}
      - RivianMate__Email__SMTP__Password=${SMTP_PASSWORD}
      - RivianMate__Email__SMTP__UseSsl=true
```

#### Gmail Setup

To use Gmail as your SMTP provider:

1. Enable 2-factor authentication on your Google account
2. Generate an App Password: Google Account → Security → App passwords
3. Use your Gmail address as `SMTP__Username`
4. Use the generated app password as `SMTP__Password`

#### Disabling Email

If you don't need email functionality, simply leave `RivianMate__Email__Enabled` unset or set to `false`. Password reset will not work, but users can still change passwords when logged in.

---

## Reverse Proxy Setup

### nginx

```nginx
server {
    listen 443 ssl http2;
    server_name rivianmate.example.com;

    ssl_certificate /etc/letsencrypt/live/rivianmate.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/rivianmate.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # Required for Blazor Server SignalR
        proxy_read_timeout 86400;
        proxy_buffering off;
    }
}
```

### Traefik

```yaml
# docker-compose.yml with Traefik labels
services:
  rivianmate:
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.rivianmate.rule=Host(`rivianmate.example.com`)"
      - "traefik.http.routers.rivianmate.tls=true"
      - "traefik.http.routers.rivianmate.tls.certresolver=letsencrypt"
      - "traefik.http.services.rivianmate.loadbalancer.server.port=8080"
```

### Caddy

```
rivianmate.example.com {
    reverse_proxy localhost:8080
}
```

---

## Upgrading

### Docker Compose

```bash
docker-compose pull
docker-compose up -d
```

### Manual

```bash
# Stop the service
sudo systemctl stop rivianmate

# Backup database
pg_dump rivianmate > backup.sql

# Download new release
wget https://github.com/yourusername/rivianmate/releases/latest/download/rivianmate-linux-x64.tar.gz

# Extract
tar -xzf rivianmate-linux-x64.tar.gz -C /opt/rivianmate

# Start service
sudo systemctl start rivianmate
```

Database migrations run automatically on startup.

---

## Backup & Restore

### Database Backup

```bash
# Using docker
docker exec rivianmate-db pg_dump -U rivianmate rivianmate > backup.sql

# Direct
pg_dump -h localhost -U rivianmate rivianmate > backup.sql
```

### Database Restore

```bash
# Using docker
docker exec -i rivianmate-db psql -U rivianmate rivianmate < backup.sql

# Direct
psql -h localhost -U rivianmate rivianmate < backup.sql
```

### Automated Backups

Add to crontab:
```bash
0 3 * * * docker exec rivianmate-db pg_dump -U rivianmate rivianmate | gzip > /backups/rivianmate-$(date +\%Y\%m\%d).sql.gz
```

---

## Troubleshooting

### Container won't start

1. **Check logs:**
   ```bash
   docker-compose logs rivianmate
   ```

2. **Database connection:**
   - Ensure PostgreSQL is running
   - Verify credentials
   - Check network connectivity

### SignalR connection issues

Blazor Server requires WebSocket support. Ensure your reverse proxy:
- Supports WebSockets
- Has adequate timeouts (>60s)
- Doesn't buffer responses

### High memory usage

The Hangfire dashboard can consume memory. If not needed:
- Access is restricted to authenticated users by default
- Consider limiting worker count in high-memory environments

### Database migrations fail

```bash
# Check migration status
docker exec rivianmate dotnet ef migrations list

# Manual migration
docker exec rivianmate dotnet ef database update
```

---

## Security Considerations

1. **Change default passwords** - Never use default PostgreSQL credentials in production

2. **Use HTTPS** - Always run behind a reverse proxy with TLS

3. **Firewall** - Only expose port 443 (HTTPS), not 8080 directly

4. **Rivian credentials** - Stored encrypted using ASP.NET Data Protection

5. **Regular updates** - Keep RivianMate and dependencies updated

---

## Community Edition Limits

The self-hosted Community Edition includes:

| Feature | Included |
|---------|----------|
| Dashboard | Yes |
| Battery Health | Yes |
| Vehicle State | Yes |
| Charging Sessions | Yes |
| Basic Polling | Yes |
| Users | 1 |
| Vehicles | 2 |
| Rivian Accounts | 1 |

For additional features (analytics, exports, notifications) or multi-user support, consider upgrading to Enterprise or using the cloud-hosted Pro edition.

---

## Getting Help

- **GitHub Issues**: Report bugs and request features
- **Discussions**: Ask questions and share configurations
- **Documentation**: Check `/docs` for additional guides
