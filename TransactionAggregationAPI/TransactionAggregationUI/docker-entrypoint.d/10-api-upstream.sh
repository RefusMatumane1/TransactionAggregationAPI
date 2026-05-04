#!/bin/sh
# Substitute ${API_UPSTREAM} in the nginx config template.
# Only this variable is substituted so that nginx $variables (e.g. $host) are preserved.
set -e
envsubst '${API_UPSTREAM}' \
  < /etc/nginx/templates/default.conf.template \
  > /etc/nginx/conf.d/default.conf
