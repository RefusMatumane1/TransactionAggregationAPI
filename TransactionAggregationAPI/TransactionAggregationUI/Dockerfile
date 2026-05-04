FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Refresh the CA certificate bundle so nuget.org TLS chain validates inside Docker Desktop on macOS.
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates openssl \
 && update-ca-certificates \
 && c_rehash /etc/ssl/certs/ \
 && rm -rf /var/lib/apt/lists/*

ENV NUGET_CERT_REVOCATION_MODE=offline
ENV DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP3SUPPORT=0

COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["TransactionAggregationUI/TransactionAggregationUI.csproj", "TransactionAggregationUI/"]
RUN dotnet restore "./TransactionAggregationUI/TransactionAggregationUI.csproj"

COPY . .
WORKDIR "/src/TransactionAggregationUI"
RUN dotnet publish "./TransactionAggregationUI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM nginx:alpine AS final
# Copy the published Blazor WASM static files
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
# nginx.conf uses ${API_UPSTREAM} — processed at startup by the entrypoint script below
COPY TransactionAggregationUI/nginx.conf /etc/nginx/templates/default.conf.template
# Entrypoint script: runs envsubst on the nginx template before nginx starts
COPY TransactionAggregationUI/docker-entrypoint.d/10-api-upstream.sh /docker-entrypoint.d/10-api-upstream.sh
RUN chmod +x /docker-entrypoint.d/10-api-upstream.sh
EXPOSE 80
