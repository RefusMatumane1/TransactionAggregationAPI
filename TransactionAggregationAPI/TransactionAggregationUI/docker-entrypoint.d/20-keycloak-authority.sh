#!/bin/sh
# Substitute ${KEYCLOAK_AUTHORITY} baked into the published wwwroot/appsettings.json (a static
# file the WASM app fetches at startup — see Program.cs) with the browser-reachable Keycloak
# realm URL for this environment. Unlike nginx.conf's template (rendered into a separate file),
# this substitutes the file in place since it's served as-is by the "/" location.
set -e
envsubst '${KEYCLOAK_AUTHORITY}' \
  < /usr/share/nginx/html/appsettings.json \
  > /usr/share/nginx/html/appsettings.json.tmp
mv /usr/share/nginx/html/appsettings.json.tmp /usr/share/nginx/html/appsettings.json
