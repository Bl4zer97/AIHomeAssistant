# ─── Build stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore
COPY AIHomeAssistant.slnx ./
COPY src/ src/
COPY tests/ tests/

RUN dotnet restore AIHomeAssistant.slnx

RUN dotnet publish src/AIHomeAssistant.Api/AIHomeAssistant.Api.csproj \
    -c Release -o /app/publish --no-restore

# ─── Runtime stage ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0-bookworm-slim AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Create writable runtime dirs, then drop to non-root user
RUN mkdir -p data logs && \
    useradd -m -u 1001 appuser && \
    chown -R appuser:appuser /app

USER appuser

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "AIHomeAssistant.Api.dll"]
