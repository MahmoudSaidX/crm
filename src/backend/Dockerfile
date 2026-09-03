# syntax=docker/dockerfile:1
# Build stage: SDK image satisfies global.json's rollForward: latestFeature
# pin to SDK 10.0.111. No digest/patch tag exists upstream for this SDK
# patch (verified with `docker manifest inspect` — CRM-205 planning); the
# combination of this floating-minor SDK tag and the repo's own global.json
# pin is the same pattern CRM-197 used for the postgres image tag.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore only the API project's own reference graph (BuildingBlocks,
# Infrastructure.*, every Modules/* project) — never the whole .sln, so the
# four test projects never enter the image or the build context.
COPY . .
RUN dotnet restore src/Api/SquadCrm.Api/SquadCrm.Api.csproj
RUN dotnet publish src/Api/SquadCrm.Api/SquadCrm.Api.csproj \
    --no-restore \
    -c Release \
    -o /app/publish

# Runtime stage: aspnet (not sdk) — smaller image, no build tooling.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl is required by the Compose healthcheck against /health/ready;
# the aspnet base image does not include it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Container-internal port. The Compose file publishes this on the same host
# port (5080) that `dotnet run` already uses, so config.json's apiBaseUrl
# needs no change.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SquadCrm.Api.dll"]
