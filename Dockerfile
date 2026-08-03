# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY TaskTrackingSystem.sln ./
COPY TaskTrackingSystem.Database/TaskTrackingSystem.Database.csproj TaskTrackingSystem.Database/
COPY TaskTrackingSystem.Shared/TaskTrackingSystem.Shared.csproj TaskTrackingSystem.Shared/
COPY TaskTrackingSystem.WebApi/TaskTrackingSystem.WebApi.csproj TaskTrackingSystem.WebApi/
COPY TaskTrackingSystem.WebApp/TaskTrackingSystem.WebApp.csproj TaskTrackingSystem.WebApp/
COPY Tools/TranslationBackfill/TranslationBackfill.csproj Tools/TranslationBackfill/
COPY Tests/TaskTrackingSystem.Tests/TaskTrackingSystem.Tests.csproj Tests/TaskTrackingSystem.Tests/

RUN dotnet restore TaskTrackingSystem.sln

COPY . .

RUN dotnet publish TaskTrackingSystem.WebApi/TaskTrackingSystem.WebApi.csproj -c $BUILD_CONFIGURATION -o /app/publish/api /p:UseAppHost=false
RUN dotnet publish TaskTrackingSystem.WebApp/TaskTrackingSystem.WebApp.csproj -c $BUILD_CONFIGURATION -o /app/publish/webapp /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 7860

RUN apt-get update \
    && apt-get install -y --no-install-recommends nginx ca-certificates \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish/api /app/api
COPY --from=build /app/publish/webapp /app/webapp
COPY nginx.conf /etc/nginx/nginx.conf
COPY start.sh /app/start.sh

RUN chmod +x /app/start.sh

CMD ["/app/start.sh"]
