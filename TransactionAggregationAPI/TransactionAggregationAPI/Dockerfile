FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS base
ARG APP_UID=1654
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:2fa828c68761b1b8c23d7662dc134421b9d3b59fe1425fdbc80804e390cdb24d AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Refresh the CA certificate bundle so nuget.org TLS chain validates inside Docker Desktop on macOS.
# openssl is needed for c_rehash which rebuilds the symlink index after update-ca-certificates.
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates openssl \
 && update-ca-certificates \
 && c_rehash /etc/ssl/certs/ \
 && rm -rf /var/lib/apt/lists/*

# Skip online CRL/OCSP revocation checks (chain validation still enforced).
# Prevents the secondary HTTPS round-trips that can surface PartialChain errors.
ENV NUGET_CERT_REVOCATION_MODE=offline
# Disable HTTP/3 (QUIC) inside Docker — falls back to TLS 1.2/1.3 reliably.
ENV DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP3SUPPORT=0

COPY ["nuget.config", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["TransactionAggregationAPI/TransactionAggregationAPI.csproj", "TransactionAggregationAPI/"]
COPY ["TransactionAggregationUI/TransactionAggregationUI.csproj", "TransactionAggregationUI/"]
COPY ["Modules/Transactions/Transactions.Application/Transactions.Application.csproj", "Modules/Transactions/Transactions.Application/"]
COPY ["Modules/Transactions/Transactions.Domain/Transactions.Domain.csproj", "Modules/Transactions/Transactions.Domain/"]
COPY ["Modules/Transactions/Transactions.Contracts/Transactions.Contracts.csproj", "Modules/Transactions/Transactions.Contracts/"]
COPY ["Modules/Transactions/Transactions.Infrastructure/Transactions.Infrastructure.csproj", "Modules/Transactions/Transactions.Infrastructure/"]
COPY ["Modules/Transactions/Transactions.Presentation/Transactions.Presentation.csproj", "Modules/Transactions/Transactions.Presentation/"]
COPY ["TransactionAggregationAPI.ServiceDefaults/TransactionAggregationAPI.ServiceDefaults.csproj", "TransactionAggregationAPI.ServiceDefaults/"]
COPY ["TransactionAggregation.Hosting/TransactionAggregation.Hosting.csproj", "TransactionAggregation.Hosting/"]
COPY ["BuildingBlocks/SharedKernel/SharedKernel.csproj", "BuildingBlocks/SharedKernel/"]
COPY ["BuildingBlocks/BuildingBlocks.Application/BuildingBlocks.Application.csproj", "BuildingBlocks/BuildingBlocks.Application/"]
COPY ["BuildingBlocks/BuildingBlocks.Persistence/BuildingBlocks.Persistence.csproj", "BuildingBlocks/BuildingBlocks.Persistence/"]
COPY ["BuildingBlocks/BuildingBlocks.Messaging/BuildingBlocks.Messaging.csproj", "BuildingBlocks/BuildingBlocks.Messaging/"]
COPY ["BuildingBlocks/BuildingBlocks.Web/BuildingBlocks.Web.csproj", "BuildingBlocks/BuildingBlocks.Web/"]
COPY ["Modules/WebhookSources/WebhookSources.Domain/WebhookSources.Domain.csproj", "Modules/WebhookSources/WebhookSources.Domain/"]
COPY ["Modules/WebhookSources/WebhookSources.Application/WebhookSources.Application.csproj", "Modules/WebhookSources/WebhookSources.Application/"]
COPY ["Modules/WebhookSources/WebhookSources.Infrastructure/WebhookSources.Infrastructure.csproj", "Modules/WebhookSources/WebhookSources.Infrastructure/"]
COPY ["Modules/WebhookSources/WebhookSources.Presentation/WebhookSources.Presentation.csproj", "Modules/WebhookSources/WebhookSources.Presentation/"]
COPY ["Modules/WebhookSources/WebhookSources.Contracts/WebhookSources.Contracts.csproj", "Modules/WebhookSources/WebhookSources.Contracts/"]
COPY ["Modules/Audit/Audit.Domain/Audit.Domain.csproj", "Modules/Audit/Audit.Domain/"]
COPY ["Modules/Audit/Audit.Application/Audit.Application.csproj", "Modules/Audit/Audit.Application/"]
COPY ["Modules/Audit/Audit.Infrastructure/Audit.Infrastructure.csproj", "Modules/Audit/Audit.Infrastructure/"]
COPY ["Modules/Audit/Audit.Presentation/Audit.Presentation.csproj", "Modules/Audit/Audit.Presentation/"]
COPY ["Modules/Audit/Audit.Contracts/Audit.Contracts.csproj", "Modules/Audit/Audit.Contracts/"]
RUN dotnet restore "./TransactionAggregationAPI/TransactionAggregationAPI.csproj"

COPY . .
WORKDIR "/src/TransactionAggregationAPI"
RUN dotnet build "./TransactionAggregationAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./TransactionAggregationAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# 8080 serves traffic; 9464 serves /metrics only (Metrics__Port) and is never published by a
# Service or Ingress. The image has no curl, so bash's /dev/tcp probes readiness.
ENV ASPNETCORE_HTTP_PORTS=8080;9464 \
    Metrics__Port=9464
EXPOSE 9464
HEALTHCHECK --interval=15s --timeout=5s --start-period=30s --retries=3 \
  CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /readiness HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && grep -qE 'Healthy|Degraded' <&3"]
ENTRYPOINT ["dotnet", "TransactionAggregationAPI.dll"]