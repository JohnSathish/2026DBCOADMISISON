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
COPY deploy/docker/web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/src/client/dist/apps /usr/share/nginx/html

EXPOSE 80
