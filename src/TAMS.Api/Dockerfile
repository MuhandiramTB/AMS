# ---------------------------------------------------------------------------
# TAMS API — multi-stage build (11 §3). The same image is built once and
# promoted Dev→CI→Test→Staging→Prod (DP-02/DP-08); config/secrets are injected
# at run time, never baked in (11 §5, 06 §9). Runs as a non-root user.
# Build context is the repository root (see deploy/docker-compose.yml).
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first (cached layer) — copy only project files, pinned SDK via global.json.
COPY global.json Directory.Build.props ./
COPY src/TAMS.Domain/TAMS.Domain.csproj src/TAMS.Domain/
COPY src/TAMS.Application/TAMS.Application.csproj src/TAMS.Application/
COPY src/TAMS.Infrastructure/TAMS.Infrastructure.csproj src/TAMS.Infrastructure/
COPY src/TAMS.Api/TAMS.Api.csproj src/TAMS.Api/
RUN dotnet restore src/TAMS.Api/TAMS.Api.csproj

# Copy the rest and publish.
COPY src/ src/
RUN dotnet publish src/TAMS.Api/TAMS.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# curl is used by the container HEALTHCHECK to probe /health/ready. Installed as
# root before dropping to the non-root app user.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as the non-root user shipped in the base image (least privilege, 06 §5).
USER $APP_UID

# Liveness/readiness probe — the API is only "healthy" once the DB is reachable.
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=5 \
    CMD curl -fsS http://localhost:8080/health/ready || exit 1

# The reverse proxy terminates TLS and forwards to Kestrel over HTTP (11 §7/§11).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "TAMS.Api.dll"]
