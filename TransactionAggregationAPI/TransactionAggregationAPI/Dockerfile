FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
ARG APP_UID=1654
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
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

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["TransactionAggregationAPI/TransactionAggregationAPI.csproj", "TransactionAggregationAPI/"]
COPY ["TransactionAggregationUI/TransactionAggregationUI.csproj", "TransactionAggregationUI/"]
COPY ["Modules/Transactions/Transactions.Application/Transactions.Application.csproj", "Modules/Transactions/Transactions.Application/"]
COPY ["Modules/Transactions/Transactions.Domain/Transactions.Domain.csproj", "Modules/Transactions/Transactions.Domain/"]
COPY ["Modules/Transactions/Transactions.Infrastructure/Transactions.Infrastructure.csproj", "Modules/Transactions/Transactions.Infrastructure/"]
COPY ["TransactionAggregationAPI.ServiceDefaults/TransactionAggregationAPI.ServiceDefaults.csproj", "TransactionAggregationAPI.ServiceDefaults/"]
COPY ["SharedKernel/SharedKernel.csproj", "SharedKernel/"]
COPY ["BuildingBlocks.Messaging/BuildingBlocks.Messaging.csproj", "BuildingBlocks.Messaging/"]
COPY ["BuildingBlocks.Web/BuildingBlocks.Web.csproj", "BuildingBlocks.Web/"]
COPY ["Modules/WebhookSources/WebhookSources.Domain/WebhookSources.Domain.csproj", "Modules/WebhookSources/WebhookSources.Domain/"]
COPY ["Modules/WebhookSources/WebhookSources.Application/WebhookSources.Application.csproj", "Modules/WebhookSources/WebhookSources.Application/"]
COPY ["Modules/WebhookSources/WebhookSources.Infrastructure/WebhookSources.Infrastructure.csproj", "Modules/WebhookSources/WebhookSources.Infrastructure/"]
COPY ["Modules/WebhookSources/WebhookSources.Contracts/WebhookSources.Contracts.csproj", "Modules/WebhookSources/WebhookSources.Contracts/"]
COPY ["Modules/BankLinks/BankLinks.Domain/BankLinks.Domain.csproj", "Modules/BankLinks/BankLinks.Domain/"]
COPY ["Modules/BankLinks/BankLinks.Application/BankLinks.Application.csproj", "Modules/BankLinks/BankLinks.Application/"]
COPY ["Modules/BankLinks/BankLinks.Infrastructure/BankLinks.Infrastructure.csproj", "Modules/BankLinks/BankLinks.Infrastructure/"]
COPY ["Modules/BankLinks/BankLinks.Contracts/BankLinks.Contracts.csproj", "Modules/BankLinks/BankLinks.Contracts/"]
COPY ["Modules/Customers/Customers.Domain/Customers.Domain.csproj", "Modules/Customers/Customers.Domain/"]
COPY ["Modules/Customers/Customers.Application/Customers.Application.csproj", "Modules/Customers/Customers.Application/"]
COPY ["Modules/Customers/Customers.Infrastructure/Customers.Infrastructure.csproj", "Modules/Customers/Customers.Infrastructure/"]
COPY ["Modules/Customers/Customers.Contracts/Customers.Contracts.csproj", "Modules/Customers/Customers.Contracts/"]
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
ENTRYPOINT ["dotnet", "TransactionAggregationAPI.dll"]