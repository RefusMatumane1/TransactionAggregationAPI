FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:6a94333d37514e385650a3c81a55e5350b67253dbe136e9cf17e499c35606a8c AS base
ARG APP_UID=1654
WORKDIR /app
# The Kafka record-signing key is persisted here. Created (as root) and handed to the app user before switching,
# so a named volume mounted on it starts out writable instead of root-owned.
RUN mkdir -p /app/App_Data && chown $APP_UID /app/App_Data
USER $APP_UID
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Same build-environment workarounds as TransactionAggregationAPI/Dockerfile — see there.
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates openssl \
 && update-ca-certificates \
 && c_rehash /etc/ssl/certs/ \
 && rm -rf /var/lib/apt/lists/*
ENV NUGET_CERT_REVOCATION_MODE=offline
ENV DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP3SUPPORT=0

# Project files first so the restore layer is cached until a dependency changes.
COPY ["nuget.config", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["TransactionAggregation.MockAggregator/TransactionAggregation.MockAggregator.csproj", "TransactionAggregation.MockAggregator/"]
COPY ["TransactionAggregationAPI.ServiceDefaults/TransactionAggregationAPI.ServiceDefaults.csproj", "TransactionAggregationAPI.ServiceDefaults/"]
RUN dotnet restore "./TransactionAggregation.MockAggregator/TransactionAggregation.MockAggregator.csproj"

COPY . .
WORKDIR "/src/TransactionAggregation.MockAggregator"
RUN dotnet build "./TransactionAggregation.MockAggregator.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TransactionAggregation.MockAggregator.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /liveness HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && grep -q 'Healthy' <&3"]
ENTRYPOINT ["dotnet", "TransactionAggregation.MockAggregator.dll"]
