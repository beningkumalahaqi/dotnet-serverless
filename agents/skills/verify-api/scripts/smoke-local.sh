#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
PORT="${PORT:-18080}"
BASE_URL="http://127.0.0.1:${PORT}"
LOG_FILE=""
PID=""

cleanup() {
  if [[ -n "$PID" ]]; then
    kill "$PID" 2>/dev/null || true
    wait "$PID" 2>/dev/null || true
  fi
}
trap cleanup EXIT

cd "$ROOT"

if ! dotnet --version >/dev/null 2>&1; then
  echo "No compatible .NET SDK. Install .NET 10 or use the Docker path in the verify-api skill." >&2
  exit 1
fi

if ! command -v curl >/dev/null 2>&1; then
  echo "curl is required to run the smoke test." >&2
  exit 1
fi

dotnet build -v quiet
LOG_FILE="$(mktemp "${TMPDIR:-/tmp}/dotnet-serverless-smoke.XXXXXX")"
PORT="$PORT" dotnet run --no-build >"$LOG_FILE" 2>&1 &
PID=$!

ready=false
for ((i = 0; i < 120; i++)); do
  if curl -fsS "$BASE_URL/health" >/dev/null 2>&1; then
    ready=true
    break
  fi
  if ! kill -0 "$PID" 2>/dev/null; then
    echo "Server exited before becoming ready. Log:" >&2
    sed -n '1,120p' "$LOG_FILE" >&2
    exit 1
  fi
  sleep 0.5
done

if [[ "$ready" != true ]]; then
  echo "Server did not become ready. Log:" >&2
  sed -n '1,120p' "$LOG_FILE" >&2
  exit 1
fi

echo "health: $(curl -fsS "$BASE_URL/health")"

openapi="$(curl -fsS "$BASE_URL/openapi/v1.json" || true)"
if [[ "$openapi" == *'"openapi"'* ]]; then
  echo "openapi: ok"
else
  echo "openapi: missing or disabled" >&2
  exit 1
fi

if [[ $# -gt 0 ]]; then
  for path in "$@"; do
    code="$(curl -sS -o /dev/null -w '%{http_code}' "$BASE_URL$path")"
    printf '%s %s\n' "$code" "$path"
  done
fi

echo "log: $LOG_FILE"
