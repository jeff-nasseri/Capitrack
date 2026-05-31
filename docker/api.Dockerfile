# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore (cached when csproj files are unchanged)
COPY Directory.Build.props ./
COPY src/Server.Domain/Server.Domain.csproj src/Server.Domain/
COPY src/Server.Application/Server.Application.csproj src/Server.Application/
COPY src/Server.Infrastructure/Server.Infrastructure.csproj src/Server.Infrastructure/
COPY src/Server.Api/Server.Api.csproj src/Server.Api/
RUN dotnet restore src/Server.Api/Server.Api.csproj

# Build + publish
COPY src/Server.Domain/ src/Server.Domain/
COPY src/Server.Application/ src/Server.Application/
COPY src/Server.Infrastructure/ src/Server.Infrastructure/
COPY src/Server.Api/ src/Server.Api/
RUN dotnet publish src/Server.Api/Server.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

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
