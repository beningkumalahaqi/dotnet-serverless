#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:8080}"

section() {
  printf '\n== %s\n' "$1"
}

section "health"
curl -sS "$BASE_URL/health"
printf '\n'

section "service metadata"
curl -sS "$BASE_URL/"
printf '\n'

section "greeting (Spanish)"
curl -sS "$BASE_URL/api/v1/examples/hello/Ada?lang=es"
printf '\n'

section "echo (valid)"
curl -sS -X POST "$BASE_URL/api/v1/examples/echo" \
  -H 'Content-Type: application/json' \
  -d '{"message":"hello from the boilerplate","priority":2}'
printf '\n'

section "echo (invalid, expect 400)"
curl -sS -i -X POST "$BASE_URL/api/v1/examples/echo" \
  -H 'Content-Type: application/json' \
  -d '{"message":"","priority":9}' \
  | sed -n '1p;/^{/p'

section "runtime"
curl -sS "$BASE_URL/api/v1/examples/runtime"
printf '\n'

section "error (expect 500)"
curl -sS -i "$BASE_URL/api/v1/examples/error" | sed -n '1p;/^{/p'

section "swagger"
curl -sS -o /dev/null -w 'HTTP %{http_code}\n' -L "$BASE_URL/swagger"

section "openapi"
curl -sS -o /dev/null -w 'HTTP %{http_code}\n' "$BASE_URL/openapi/v1.json"
