FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore Otklik.slnx

FROM build AS publish
RUN dotnet publish src/backend/Otklik.Worker/Otklik.Worker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache krb5-libs
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
HEALTHCHECK --interval=15s --timeout=3s --start-period=20s --retries=5 \
    CMD test -f /tmp/worker-ready || exit 1
ENTRYPOINT ["dotnet", "Otklik.Worker.dll"]
