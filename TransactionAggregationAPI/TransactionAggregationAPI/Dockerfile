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
COPY ["TransactionAggregation.Application/TransactionAggregation.Application.csproj", "TransactionAggregation.Application/"]
COPY ["TransactionAggregation.Domain/TransactionAggregation.Domain.csproj", "TransactionAggregation.Domain/"]
COPY ["TransactionAggregation.Infrastructure/TransactionAggregation.Infrastructure.csproj", "TransactionAggregation.Infrastructure/"]
COPY ["TransactionAggregation.Persistence/TransactionAggregation.Persistence.csproj", "TransactionAggregation.Persistence/"]
COPY ["TransactionAggregationAPI.ServiceDefaults/TransactionAggregationAPI.ServiceDefaults.csproj", "TransactionAggregationAPI.ServiceDefaults/"]
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