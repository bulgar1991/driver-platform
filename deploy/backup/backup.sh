#!/bin/sh
# One compressed dump of the database, checked, then old dumps pruned.
# Uses the standard PG* environment variables (PGHOST, PGUSER, PGPASSWORD, PGDATABASE, PGPORT).
set -eu

BACKUP_DIR="${BACKUP_DIR:-./backups}"
BACKUP_KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"
mkdir -p "$BACKUP_DIR"

stamp=$(date -u +%Y%m%d-%H%M%S)
file="$BACKUP_DIR/${PGDATABASE}-$stamp.dump"
tmp="$file.partial"

pg_dump --format=custom --compress=9 --no-owner --file="$tmp"
# A dump that cannot be listed is not a backup.
pg_restore --list "$tmp" > /dev/null
mv "$tmp" "$file"
echo "backup: wrote $file ($(du -h "$file" | cut -f1))"

find "$BACKUP_DIR" -name "${PGDATABASE}-*.dump" -type f -mtime +"$BACKUP_KEEP_DAYS" -print -delete \
    | sed 's/^/backup: removed /'
