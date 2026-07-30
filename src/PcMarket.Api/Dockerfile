# Build context is the repository root: this image needs the shared props files and the whole
# backend project graph. Build with:
#   docker build -f src/PcMarket.Api/Dockerfile -t pcmarket-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first from just the project files, so the (slow) restore layer is reused until a
# dependency actually changes.
COPY Directory.Build.props Directory.Packages.props ./
COPY src/PcMarket.Domain/PcMarket.Domain.csproj src/PcMarket.Domain/
COPY src/PcMarket.Contracts/PcMarket.Contracts.csproj src/PcMarket.Contracts/
COPY src/PcMarket.Application/PcMarket.Application.csproj src/PcMarket.Application/
COPY src/PcMarket.Infrastructure/PcMarket.Infrastructure.csproj src/PcMarket.Infrastructure/
COPY src/PcMarket.Payments/PcMarket.Payments.csproj src/PcMarket.Payments/
COPY src/PcMarket.Bot/PcMarket.Bot.csproj src/PcMarket.Bot/
COPY src/PcMarket.Api/PcMarket.Api.csproj src/PcMarket.Api/
RUN dotnet restore src/PcMarket.Api/PcMarket.Api.csproj

COPY src/ src/

# Not --no-restore: it makes publish emit a static-web-assets manifest missing the framework assets
# (see PcMarket.Web/Dockerfile). The API has less to lose than the Blazor hosts, but the failure mode
# is silent, so all three images publish the same way.
RUN dotnet publish src/PcMarket.Api/PcMarket.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is here purely so the container can answer its own compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# Drop privileges: the app never needs to write to its own image.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "PcMarket.Api.dll"]
