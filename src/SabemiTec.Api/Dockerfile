FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY SabemiTec.sln ./
COPY src/SabemiTec.Api/SabemiTec.Api.csproj src/SabemiTec.Api/
COPY tests/SabemiTec.Tests/SabemiTec.Tests.csproj tests/SabemiTec.Tests/
RUN dotnet restore src/SabemiTec.Api/SabemiTec.Api.csproj

COPY src/SabemiTec.Api/ src/SabemiTec.Api/
RUN dotnet publish src/SabemiTec.Api/SabemiTec.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080

# Render (and most PaaS) inject $PORT at runtime and expect the container to bind to it;
# docker-compose never sets PORT, so this falls back to 8080, matching the compose config.
# `exec` keeps dotnet as PID 1 so it receives SIGTERM directly for graceful shutdown.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://+:${PORT:-8080} exec dotnet SabemiTec.Api.dll"]
