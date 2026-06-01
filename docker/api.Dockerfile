# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (cached when csproj files are unchanged)
COPY Directory.Build.props ./
COPY src/server/Server.Domain/Server.Domain.csproj src/server/Server.Domain/
COPY src/server/Server.Application/Server.Application.csproj src/server/Server.Application/
COPY src/server/Server.Infrastructure/Server.Infrastructure.csproj src/server/Server.Infrastructure/
COPY src/server/Server.Api/Server.Api.csproj src/server/Server.Api/
RUN dotnet restore src/server/Server.Api/Server.Api.csproj

# Build + publish
COPY src/server/Server.Domain/ src/server/Server.Domain/
COPY src/server/Server.Application/ src/server/Server.Application/
COPY src/server/Server.Infrastructure/ src/server/Server.Infrastructure/
COPY src/server/Server.Api/ src/server/Server.Api/
RUN dotnet publish src/server/Server.Api/Server.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

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

ENTRYPOINT ["dotnet", "Server.Api.dll"]
