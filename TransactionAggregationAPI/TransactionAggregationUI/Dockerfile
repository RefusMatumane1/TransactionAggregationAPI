FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
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

COPY ["nuget.config", "."]
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["TransactionAggregationUI/TransactionAggregationUI.csproj", "TransactionAggregationUI/"]
RUN dotnet restore "./TransactionAggregationUI/TransactionAggregationUI.csproj"

COPY . .
WORKDIR "/src/TransactionAggregationUI"
RUN dotnet publish "./TransactionAggregationUI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# nginx-unprivileged runs master and workers as uid 101 — nothing in this container runs as
# root, so it listens on 8080 (ports < 1024 need root). The entrypoint scripts rewrite
# default.conf and appsettings.json at startup, so those paths are owned by that user.
FROM nginxinc/nginx-unprivileged:1.27-alpine@sha256:65e3e85dbaed8ba248841d9d58a899b6197106c23cb0ff1a132b7bfe0547e4c0 AS final
# The base image runs as uid 101 but leaves the web root owned by root; the Keycloak
# entrypoint script rewrites appsettings.json in place, so the directory must be writable.
USER root
RUN chown 101:0 /usr/share/nginx/html
USER 101
COPY --from=build --chown=101:0 /app/publish/wwwroot /usr/share/nginx/html
# nginx.conf uses ${API_UPSTREAM} — processed at startup by the entrypoint script below
COPY --chown=101:0 TransactionAggregationUI/nginx.conf /etc/nginx/templates/default.conf.template
# Entrypoint scripts: run envsubst on the nginx template and on the published
# appsettings.json (Keycloak authority) before nginx starts.
COPY --chown=101:0 --chmod=0755 TransactionAggregationUI/docker-entrypoint.d/10-api-upstream.sh /docker-entrypoint.d/10-api-upstream.sh
COPY --chown=101:0 --chmod=0755 TransactionAggregationUI/docker-entrypoint.d/20-keycloak-authority.sh /docker-entrypoint.d/20-keycloak-authority.sh
USER 101
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --retries=3 CMD wget -q -O /dev/null http://127.0.0.1:8080/ || exit 1
