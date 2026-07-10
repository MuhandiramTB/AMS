# ---------------------------------------------------------------------------
# TAMS Worker — the ZKTeco capture service (ADR-002), a separate deployable
# from the API (11 §3). Multi-stage build; non-root; no HTTP surface (it polls
# devices and writes to the DB). Build context is the repository root.
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/TAMS.Domain/TAMS.Domain.csproj src/TAMS.Domain/
COPY src/TAMS.Application/TAMS.Application.csproj src/TAMS.Application/
COPY src/TAMS.Infrastructure/TAMS.Infrastructure.csproj src/TAMS.Infrastructure/
COPY src/TAMS.Worker/TAMS.Worker.csproj src/TAMS.Worker/
RUN dotnet restore src/TAMS.Worker/TAMS.Worker.csproj

COPY src/ src/
RUN dotnet publish src/TAMS.Worker/TAMS.Worker.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# No ASP.NET runtime needed — the Worker is a plain .NET host.
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
USER $APP_UID

COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "TAMS.Worker.dll"]
