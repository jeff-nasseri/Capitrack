# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (cached when csproj is unchanged)
COPY src/Capitrack.Api/Capitrack.Api.csproj src/Capitrack.Api/
RUN dotnet restore src/Capitrack.Api/Capitrack.Api.csproj

# Build + publish
COPY src/Capitrack.Api/ src/Capitrack.Api/
RUN dotnet publish src/Capitrack.Api/Capitrack.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/data /app/transactions

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV DB_PATH=/app/data/capitrack.db
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "Capitrack.Api.dll"]
