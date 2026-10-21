#!/bin/sh
# Restores a dump into a database. Refuses to overwrite a database that has tables unless --force.
#   PGHOST=… PGUSER=… PGPASSWORD=… ./restore.sh backups/driver_platform-20261021-031500.dump driver_platform_restore
set -eu

dump="${1:?usage: restore.sh <file.dump> <database> [--force]}"
target="${2:?usage: restore.sh <file.dump> <database> [--force]}"
force="${3:-}"

pg_restore --list "$dump" > /dev/null

exists=$(psql -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$target'")
if [ "$exists" = "1" ]; then
    tables=$(psql -d "$target" -tAc "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'")
    if [ "$tables" != "0" ] && [ "$force" != "--force" ]; then
        echo "restore: $target already has $tables tables; add --force to replace them" >&2
        exit 1
    fi
else
    createdb "$target"
fi

pg_restore --clean --if-exists --no-owner --exit-on-error --dbname="$target" "$dump"
echo "restore: $dump -> $target"
psql -d "$target" -tAc "SELECT 'restore: ' || count(*) || ' bookings, ' || (SELECT count(*) FROM invoices) || ' invoices' FROM bookings"
