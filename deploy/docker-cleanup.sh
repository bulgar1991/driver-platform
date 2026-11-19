#!/bin/sh
# Weekly on the server (/etc/cron.d/docker-cleanup): removes Docker build leftovers that every
# rebuild adds, keeping 2 GB of recent build cache so the next deploy stays fast. Never touches
# volumes (database, HTTPS certificates), backups or the images of running containers.
# Only untagged images go: replaced builds of the site lose their tag and are removed, while the
# build images (.NET SDK, Node, Caddy) stay, so a deploy never has to download them again.
#   Install: cp deploy/docker-cleanup.sh /usr/local/sbin/ && chmod 755 /usr/local/sbin/docker-cleanup.sh
#            echo '30 4 * * 0 root /usr/local/sbin/docker-cleanup.sh >> /var/log/docker-cleanup.log 2>&1' > /etc/cron.d/docker-cleanup
set -eu
echo "== $(date -u '+%Y-%m-%d %H:%M UTC')"
docker builder prune -f --keep-storage 2gb | tail -1
docker image prune -f | tail -1
# The images the Dockerfiles build FROM, kept as tagged images (the cleanup above leaves those
# alone). Pulling them here, on Sunday night, also fetches their updates ahead of the next deploy.
for image in mcr.microsoft.com/dotnet/sdk:10.0 mcr.microsoft.com/dotnet/aspnet:10.0 \
  node:24-alpine caddy:2-alpine; do
  docker pull -q "$image" || echo "could not pull $image"
done
df -h / | tail -1
