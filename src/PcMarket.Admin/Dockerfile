# Build context is the repository root. Build with:
#   docker build -f src/PcMarket.Admin/Dockerfile -t pcmarket-admin .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./
COPY src/PcMarket.Contracts/PcMarket.Contracts.csproj src/PcMarket.Contracts/
COPY shared/PcMarket.ApiClient/PcMarket.ApiClient.csproj shared/PcMarket.ApiClient/
COPY src/PcMarket.Admin/PcMarket.Admin.csproj src/PcMarket.Admin/
RUN dotnet restore src/PcMarket.Admin/PcMarket.Admin.csproj

COPY src/ src/
COPY shared/ shared/

# See the note in PcMarket.Web/Dockerfile: --no-restore here drops wwwroot/_framework/blazor.web.js
# and the panel renders blank.
RUN dotnet publish src/PcMarket.Admin/PcMarket.Admin.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "PcMarket.Admin.dll"]
