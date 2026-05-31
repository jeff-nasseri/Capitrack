# ---- Build stage (publish Blazor WASM to static files) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Capitrack.Web/Capitrack.Web.csproj src/Capitrack.Web/
RUN dotnet restore src/Capitrack.Web/Capitrack.Web.csproj

COPY src/Capitrack.Web/ src/Capitrack.Web/
RUN dotnet publish src/Capitrack.Web/Capitrack.Web.csproj -c Release -o /app/publish

# ---- Runtime stage (nginx serves the WASM app + proxies /api) ----
FROM nginx:alpine
COPY docker/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
