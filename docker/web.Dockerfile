# ---- Build stage (publish Blazor WASM to static files) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the full client project graph (Domain -> Application -> Infrastructure -> Presentation).
COPY src/Client.Domain/Client.Domain.csproj src/Client.Domain/
COPY src/Client.Application/Client.Application.csproj src/Client.Application/
COPY src/Client.Infrastructure/Client.Infrastructure.csproj src/Client.Infrastructure/
COPY src/Client.Presentation/Client.Presentation.csproj src/Client.Presentation/
RUN dotnet restore src/Client.Presentation/Client.Presentation.csproj

COPY src/Client.Domain/ src/Client.Domain/
COPY src/Client.Application/ src/Client.Application/
COPY src/Client.Infrastructure/ src/Client.Infrastructure/
COPY src/Client.Presentation/ src/Client.Presentation/
RUN dotnet publish src/Client.Presentation/Client.Presentation.csproj -c Release -o /app/publish

# ---- Runtime stage (nginx serves the WASM app + proxies /api) ----
FROM nginx:alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
