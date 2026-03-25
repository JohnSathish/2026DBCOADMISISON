# Applicant portal (Angular) — static files behind nginx
FROM node:20-bookworm-slim AS build
WORKDIR /app

# Workspace: Nx + Angular live under src/client
COPY src/client ./src/client
WORKDIR /app/src/client

RUN corepack enable pnpm \
    && pnpm install --frozen-lockfile \
    && pnpm exec nx build applicant-portal --configuration=production

FROM nginx:1.27-alpine
RUN apk add --no-cache gettext
# /docker-entrypoint.d/99-api-upstream.sh writes conf.d/default.conf from template (envsubst).
ENV API_UPSTREAM_HOST=api
COPY deploy/docker/web/default.conf.template /etc/nginx/api/default.conf.template
COPY deploy/docker/web/docker-entrypoint.d/99-api-upstream.sh /docker-entrypoint.d/99-api-upstream.sh
RUN chmod +x /docker-entrypoint.d/99-api-upstream.sh \
    && sed -i 's/\r$//' /docker-entrypoint.d/99-api-upstream.sh
COPY --from=build /app/src/client/dist/apps /usr/share/nginx/html

EXPOSE 80
