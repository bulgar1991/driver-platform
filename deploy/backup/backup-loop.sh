#!/bin/sh
# Runs backup.sh once at start, then every day at BACKUP_AT (HH:MM, container time = UTC).
set -eu
BACKUP_AT="${BACKUP_AT:-03:15}"

/bin/sh /scripts/backup.sh || echo "backup: FAILED at start" >&2
while true; do
    now=$(date -u +%s)
    target=$(date -u -d "$(date -u +%Y-%m-%d) $BACKUP_AT" +%s 2>/dev/null || date -u -D "%Y-%m-%d %H:%M" -d "$(date -u +%Y-%m-%d) $BACKUP_AT" +%s)
    [ "$target" -le "$now" ] && target=$((target + 86400))
    sleep $((target - now))
    /bin/sh /scripts/backup.sh || echo "backup: FAILED" >&2
done
