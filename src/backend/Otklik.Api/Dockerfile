FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore Otklik.slnx

FROM build AS test
RUN dotnet test Otklik.slnx --configuration Release --no-restore

FROM build AS integration-test
ENV OTKLIK_INTEGRATION_BASE_URL=http://api:8080/
ENTRYPOINT ["dotnet", "test", "tests/backend/Otklik.IntegrationTests/Otklik.IntegrationTests.csproj", "--configuration", "Release", "--no-restore"]

FROM build AS publish
RUN dotnet publish src/backend/Otklik.Api/Otklik.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
RUN apk add --no-cache curl krb5-libs \
    && mkdir -p /var/lib/otklik/keys /var/lib/otklik/attachments \
    && chown -R $APP_UID:$APP_UID /var/lib/otklik
WORKDIR /app
COPY --from=publish /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Otklik.Api.dll"]
