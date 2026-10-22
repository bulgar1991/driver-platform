#!/usr/bin/env bash
# Starts the whole platform for development: the API on http://localhost:5000, then the site in
# all four languages on http://localhost:4200. Ctrl+C stops both.
#
#   ./dev.sh
#
# Needs PostgreSQL on localhost:5432 (backend/src/DriverPlatform.Api/appsettings.Development.json);
# the API creates and fills the database on its first start.
set -euo pipefail

root="$(cd "$(dirname "$0")" && pwd)"
api_url="http://localhost:5000/api/health"

# The .NET SDK is installed in ~/.dotnet, which is not always on PATH.
if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
fi
command -v dotnet >/dev/null 2>&1 || { echo "dotnet not found: install the .NET 10 SDK." >&2; exit 1; }
command -v npm >/dev/null 2>&1 || { echo "npm not found: install Node.js." >&2; exit 1; }

for port in 5000 4200; do
  if lsof -iTCP:"$port" -sTCP:LISTEN >/dev/null 2>&1; then
    echo "Port $port is already in use. Stop what runs there (an earlier ./dev.sh or npm start) and try again." >&2
    exit 1
  fi
done

pids=()
stop() {
  trap - INT TERM EXIT
  echo
  echo "Stopping…"
  for pid in "${pids[@]}"; do
    # The whole process group: dotnet run and npm start each start child processes.
    kill -TERM "-$pid" 2>/dev/null || kill -TERM "$pid" 2>/dev/null || true
  done
  wait 2>/dev/null || true
}
trap stop INT TERM EXIT

# Each part in its own process group, its output prefixed so the two can be told apart.
set -m
(
  cd "$root/backend"
  # macOS .NET reaches TLS 1.3-only map servers only through the system network stack.
  DOTNET_SYSTEM_NET_SECURITY_USENETWORKFRAMEWORK=1 \
    dotnet run --project src/DriverPlatform.Api 2>&1 | sed -u 's/^/[api] /'
) &
pids+=($!)

echo "Starting the API…"
for _ in $(seq 1 120); do
  if curl -fs "$api_url" >/dev/null 2>&1; then
    echo "API ready on http://localhost:5000"
    break
  fi
  if ! kill -0 "${pids[0]}" 2>/dev/null; then
    echo "The API stopped: see the [api] lines above (is PostgreSQL running on localhost:5432?)." >&2
    exit 1
  fi
  sleep 1
done
curl -fs "$api_url" >/dev/null 2>&1 || echo "The API is still not answering; starting the site anyway." >&2

(
  cd "$root/frontend"
  [ -d node_modules ] || npm install
  npm start 2>&1 | sed -u 's/^/[web] /'
) &
pids+=($!)
set +m

wait
