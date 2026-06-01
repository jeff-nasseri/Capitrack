# ---- Build stage (publish Blazor WASM to static files) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore against the full client project graph (Domain -> Application -> Infrastructure -> Presentation).
COPY src/client/Client.Domain/Client.Domain.csproj src/client/Client.Domain/
COPY src/client/Client.Application/Client.Application.csproj src/client/Client.Application/
COPY src/client/Client.Infrastructure/Client.Infrastructure.csproj src/client/Client.Infrastructure/
COPY src/client/Client.Presentation/Client.Presentation.csproj src/client/Client.Presentation/
RUN dotnet restore src/client/Client.Presentation/Client.Presentation.csproj

COPY src/client/Client.Domain/ src/client/Client.Domain/
COPY src/client/Client.Application/ src/client/Client.Application/
COPY src/client/Client.Infrastructure/ src/client/Client.Infrastructure/
COPY src/client/Client.Presentation/ src/client/Client.Presentation/
RUN dotnet publish src/client/Client.Presentation/Client.Presentation.csproj -c Release -o /app/publish

# ---- Runtime stage (nginx serves the WASM app + proxies /api) ----
FROM nginx:alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
