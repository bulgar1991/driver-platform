#!/bin/sh
# Weekly on the server (/etc/cron.d/docker-cleanup): removes Docker build leftovers that every
# rebuild adds, keeping 2 GB of recent build cache so the next deploy stays fast. Never touches
# volumes (database, HTTPS certificates), backups or the images of running containers.
#   Install: cp deploy/docker-cleanup.sh /usr/local/sbin/ && chmod 755 /usr/local/sbin/docker-cleanup.sh
#            echo '30 4 * * 0 root /usr/local/sbin/docker-cleanup.sh >> /var/log/docker-cleanup.log 2>&1' > /etc/cron.d/docker-cleanup
set -eu
echo "== $(date -u '+%Y-%m-%d %H:%M UTC')"
docker builder prune -f --keep-storage 2gb | tail -1
docker image prune -af | tail -1
df -h / | tail -1
