#!/bin/sh
set -e
# Substitute API_UPSTREAM_HOST into nginx config (Docker DNS name of API container on Coolify network).
if [ -z "$API_UPSTREAM_HOST" ]; then
  echo "ERROR: API_UPSTREAM_HOST is not set. Set it in Coolify (web service) to the API container hostname (see deploy/DEPLOY_STEPS.md)." >&2
  exit 1
fi
if ! command -v envsubst >/dev/null 2>&1; then
  echo "ERROR: envsubst not found" >&2
  exit 1
fi
envsubst '${API_UPSTREAM_HOST}' < /etc/nginx/api/default.conf.template > /etc/nginx/conf.d/default.conf
