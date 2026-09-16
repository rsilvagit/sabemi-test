FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY src/SabemiTec.LoadSimulator/SabemiTec.LoadSimulator.csproj src/SabemiTec.LoadSimulator/
RUN dotnet restore src/SabemiTec.LoadSimulator/SabemiTec.LoadSimulator.csproj

COPY src/SabemiTec.LoadSimulator/ src/SabemiTec.LoadSimulator/
RUN dotnet publish src/SabemiTec.LoadSimulator/SabemiTec.LoadSimulator.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID

# No inbound port — this is a background worker, not an HTTP service. It calls out to
# API_URL (the sabemi-api public URL) using WEBHOOK_API_KEY, same as a real bank webhook.
ENTRYPOINT ["dotnet", "SabemiTec.LoadSimulator.dll"]
