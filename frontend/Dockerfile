# Site image: the Angular build served by Caddy, which also terminates HTTPS and proxies /api.
# Build from the repository root: docker build -f frontend/Dockerfile --build-arg SITE_DOMAIN=example.it -t magnus-web .
# SITE_DOMAIN goes into the prerendered pages (canonical links, link previews) and the sitemap.
FROM node:24-alpine AS build
ARG SITE_DOMAIN
# The client the site is for: a folder in frontend/src/brands/ (name, logo, contacts, icons).
ARG BRAND=magnus-drive
# The language at the root of the site (it, en, de or fr); the others go under /<code>.
ARG SITE_LANGUAGE=en
# Where trips are served: "europe" or ISO country codes, e.g. "it,fr,ch,de".
ARG SERVICE_AREA=europe
# The company's time zone ("today" in the panel); ride times use the pickup's own zone.
ARG TIME_ZONE=Europe/Rome
RUN test -n "$SITE_DOMAIN" || (echo 'Build argument SITE_DOMAIN is required' && exit 1)
ENV SITE_URL=https://${SITE_DOMAIN} BRAND=${BRAND} SITE_LANGUAGE=${SITE_LANGUAGE} SERVICE_AREA=${SERVICE_AREA} TIME_ZONE=${TIME_ZONE}
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --ignore-scripts --no-audit --no-fund
COPY frontend/ ./
RUN npm run build:site

FROM caddy:2-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /app/dist/frontend/browser /srv
