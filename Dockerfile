# ERP.Api — .NET 8, Kestrel on port 8080 (Coolify / Docker)
# Same as deploy/docker/api/Dockerfile. Placed at repo root so Coolify "Dockerfile" default path works.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ERP.sln ./
COPY src/server/Domain/ ./src/server/Domain/
COPY src/server/Application/ ./src/server/Application/
COPY src/server/Infrastructure/ ./src/server/Infrastructure/
COPY src/server/Api/ ./src/server/Api/

RUN dotnet publish src/server/Api/ERP.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "ERP.Api.dll"]
