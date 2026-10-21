# Site image: the Angular build served by Caddy, which also terminates HTTPS and proxies /api.
# Build from the repository root: docker build -f frontend/Dockerfile -t magnus-web .
FROM node:24-alpine AS build
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund
COPY frontend/ ./
RUN npx ng build --configuration production

FROM caddy:2-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist/frontend/browser /srv
