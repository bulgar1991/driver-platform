#!/bin/sh
# Updates the site: a backup of the database, the latest code, a rebuild, then the build leftovers
# removed (docker-cleanup.sh) so the disk does not fill up deploy after deploy.
#   Run on the server, from the deploy folder:  sh deploy.sh
#   In the background (survives a closed SSH session):
#     nohup sh deploy.sh > /tmp/deploy.log 2>&1 < /dev/null &
# The last line of the log is "DEPLOY EXIT 0" when everything went well.
set -u
cd "$(dirname "$0")"
compose="docker compose --env-file .env"

finish() {
  echo "DEPLOY EXIT $1"
  exit "$1"
}

echo "== $(date -u '+%Y-%m-%d %H:%M UTC') deploy"
df -h / | tail -1

mkdir -p backups
backup="backups/driver_platform-$(date +%Y%m%d-%H%M%S)-predeploy.dump"
if ! $compose exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB"' > "$backup" || [ ! -s "$backup" ]; then
  echo "backup failed: nothing deployed"
  rm -f "$backup"
  finish 1
fi
echo "backup: $backup ($(du -h "$backup" | cut -f1))"

git -C .. pull --ff-only || finish 1
git -C .. log --oneline -1

$compose up -d --build || finish 1

# Not a reason to fail the deploy: the site is already up.
sh ./docker-cleanup.sh || echo "cleanup failed (the site is up)"
finish 0
