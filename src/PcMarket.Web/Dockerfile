# Build context is the repository root. Build with:
#   docker build -f src/PcMarket.Web/Dockerfile -t pcmarket-web .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/PcMarket.Contracts/PcMarket.Contracts.csproj src/PcMarket.Contracts/
COPY shared/PcMarket.ApiClient/PcMarket.ApiClient.csproj shared/PcMarket.ApiClient/
COPY src/PcMarket.Web/PcMarket.Web.csproj src/PcMarket.Web/
RUN dotnet restore src/PcMarket.Web/PcMarket.Web.csproj

COPY src/ src/
COPY shared/ shared/

# Deliberately NOT --no-restore. With it, publish silently emits a static-web-assets manifest missing
# the framework assets — no warning, exit 0 — so wwwroot/_framework/blazor.web.js is absent and every
# page renders blank (prerender is off, so that script *is* the page). The restore above still pays
# for itself: its packages are already in the image, making this pass a fast no-op.
RUN dotnet publish src/PcMarket.Web/PcMarket.Web.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "PcMarket.Web.dll"]
