# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore
COPY src/RivianMate.Core/RivianMate.Core.csproj RivianMate.Core/
COPY src/RivianMate.Shared/RivianMate.Shared.csproj RivianMate.Shared/
COPY src/RivianMate.Infrastructure/RivianMate.Infrastructure.csproj RivianMate.Infrastructure/
COPY src/RivianMate.Api/RivianMate.Api.csproj RivianMate.Api/

RUN dotnet restore RivianMate.Api/RivianMate.Api.csproj

# Copy everything and build
COPY src/ .
RUN dotnet build RivianMate.Api/RivianMate.Api.csproj -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish RivianMate.Api/RivianMate.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN groupadd -r rivianmate && useradd -r -g rivianmate rivianmate

# Copy published app
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R rivianmate:rivianmate /app

# Switch to non-root user
USER rivianmate

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

ENTRYPOINT ["dotnet", "RivianMate.Api.dll"]
