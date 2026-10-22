# Site image: the Angular build served by Caddy, which also terminates HTTPS and proxies /api.
# Build from the repository root: docker build -f frontend/Dockerfile --build-arg SITE_DOMAIN=example.it -t magnus-web .
# SITE_DOMAIN goes into the prerendered pages (canonical links, link previews) and the sitemap.
FROM node:24-alpine AS build
ARG SITE_DOMAIN
RUN test -n "$SITE_DOMAIN" || (echo 'Build argument SITE_DOMAIN is required' && exit 1)
ENV SITE_URL=https://${SITE_DOMAIN}
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund
COPY frontend/ ./
RUN npm run build:site

FROM caddy:2-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist/frontend/browser /srv
