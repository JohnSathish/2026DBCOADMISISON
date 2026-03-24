# ERP.Api — .NET 8, Kestrel on port 8080 (Coolify / Docker)
# Kept in sync with /Dockerfile at repo root (Coolify default path uses root Dockerfile).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
ENV NUGET_XMLDOC_MODE=skip

COPY ERP.sln ./
COPY src/server/Domain/ ./src/server/Domain/
COPY src/server/Application/ ./src/server/Application/
COPY src/server/Infrastructure/ ./src/server/Infrastructure/
COPY src/server/Api/ ./src/server/Api/

RUN dotnet restore src/server/Api/ERP.Api.csproj --disable-parallel \
    && dotnet publish src/server/Api/ERP.Api.csproj -c Release -o /app/publish --no-restore -m:1

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ERP.Api.dll"]
