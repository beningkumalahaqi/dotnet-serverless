---
name: verify-api
description: Build, run, and smoke-test the dotnet-serverless API locally or in Docker. Use after changing endpoints, configuration, or the container, or when a route returns 404/500/502 or OpenAPI/Swagger looks wrong. Covers build, PORT handling, curl checks, OpenAPI validation, and cleanup.
---

# Verify the API

This project has no test project by design. Verification means building, starting
the app on a known port, and exercising the real HTTP surface. Do not claim a
change works from a successful compile alone.

## 1. Check the toolchain

From the repository root:

```bash
dotnet --version
```

`global.json` pins SDK `10.0.100` with `rollForward: latestFeature`. If the
command fails with "A compatible .NET SDK was not found", install .NET 10 or use
the Docker path below. Do not edit `global.json` down to an installed SDK just to
get a build.

## 2. Build

```bash
dotnet build
```

Fix the root cause of warnings that matter; do not suppress errors to get a
green build. The project namespace is `DotnetServerless`, the assembly is `App`,
and the app targets `net10.0`.

## 3. Run on a safe port

The app binds `0.0.0.0:$PORT` and defaults to `8080`. Use a different port when a
server may already be running:

```bash
PORT=18080 dotnet run --no-build
```

Run it in the background or a second terminal. The startup line should contain:

```text
Now listening on: http://0.0.0.0:18080
```

Do not use `ASPNETCORE_URLS` to change the port; `ConfigureServerlessHost` sets
the binding from `PORT`. Do not bind `localhost`/`127.0.0.1` in application code.

A convenience script is included:

```bash
PORT=18080 agents/skills/verify-api/scripts/smoke-local.sh
```

It builds, starts the server, waits for `/health`, checks the OpenAPI document,
and cleans up the process. Pass route paths as arguments to probe them:

```bash
PORT=18080 agents/skills/verify-api/scripts/smoke-local.sh \
  /api/v1/examples/hello/Ada
```

## 4. Run the smoke matrix

Set `ROUTE`, `OPENAPI_PATH`, and `BODY` for the endpoint under test:

```bash
ROUTE=/api/v1/text/uppercase
OPENAPI_PATH="$ROUTE"
BODY='{"text":"hello"}'

# Liveness: 200 {"status":"ok"}
curl -fsS http://localhost:18080/health

# Service metadata and links
curl -fsS http://localhost:18080/

# Happy path
curl -i -X POST "http://localhost:18080$ROUTE" \
  -H 'Content-Type: application/json' \
  -d "$BODY"

# Invalid body: 400 application/problem+json with "errors"
curl -i -X POST "http://localhost:18080$ROUTE" \
  -H 'Content-Type: application/json' \
  -d '{"text":""}'

# Unknown route: 404 problem+json, not an HTML error page
curl -i http://localhost:18080/api/v1/does-not-exist

# OpenAPI contains the route
curl -s http://localhost:18080/openapi/v1.json \
  | jq -e --arg path "$OPENAPI_PATH" '.paths[$path]'

# Swagger UI follows a redirect to index.html
curl -fsS -L -o /dev/null -w '%{http_code}\n' http://localhost:18080/swagger
```

For a `GET` route, change `-X POST` and drop the body. For a path parameter, use a
concrete value in `ROUTE` but keep the template in `OPENAPI_PATH`, for example
`ROUTE=/api/v1/examples/hello/Ada` and
`OPENAPI_PATH=/api/v1/examples/hello/{name}`. Query strings belong in `ROUTE`
only; they are not part of OpenAPI paths.

Expected baseline:

| Check | Expected |
|-------|----------|
| `GET /health` | 200, `{"status":"ok"}` |
| `GET /` | 200 with service name, version, environment, links |
| Valid request | The documented success status and body |
| Invalid request | 400, `application/problem+json`, `errors` object |
| Unknown route | 404 ProblemDetails |
| `/openapi/v1.json` | 200 and the new path present |
| `/swagger` | 200 after redirect |

When `Docs__Enabled=false`, `/swagger` and `/openapi/v1.json` must return 404,
and `GET /` must omit the `docs` and `openapi` links.

```bash
Docs__Enabled=false PORT=18080 dotnet run --no-build
```

## 5. Docker path

Use this when the .NET 10 SDK is unavailable or the container itself changed:

```bash
docker build -t dotnet-serverless .
docker run --rm -p 18080:8080 -e PORT=8080 dotnet-serverless
```

Then run the same smoke matrix against `http://localhost:18080`. The image is
chiseled and non-root: it has no shell, `curl`, or package manager, so do not try
to `docker exec` into it for checks. Use the platform HTTP health check or a
temporary non-chiseled image for debugging.

Confirm graceful shutdown by stopping the container; it should exit well within
the 30-second platform grace period.

## 6. Triage failures

| Symptom | Likely cause |
|---------|--------------|
| New route returns 404 | Group not registered in `MapApiEndpoints`, wrong prefix, or wrong method |
| 405 | Path matches but the HTTP method does not |
| Valid payload returns 400 | Missing/invalid DataAnnotations, malformed JSON, or a required query/route parameter |
| 500 | Unhandled exception; read the console log, then fix the handler |
| Swagger 404 | `Docs:Enabled` is false |
| OpenAPI missing a schema | Handler returns an anonymous type or lacks `Produces<T>()` |
| Deployment returns 502 | Platform target port and `PORT` do not match, or the app binds loopback |
| Upload returns 413 | Vercel's ~4.5 MB edge limit; do not raise Kestrel limits |

More background: `docs/troubleshooting.md`.

## 7. Clean up

Stop the server and confirm the port is free:

```bash
lsof -nP -iTCP:18080 -sTCP:LISTEN
```

Do not leave background servers or Docker containers running after verification.
Remove temporary log files you created.

## Report

State the exact commands run and the observed status/body for each check. If a
check could not run (for example no .NET 10 SDK and no Docker), say so explicitly
instead of implying success.
