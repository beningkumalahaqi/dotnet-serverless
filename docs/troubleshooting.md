# Troubleshooting

Symptoms are ordered by how often they bite a first deployment.

## Every request returns 502

**Cause:** The app is not listening where the platform sends traffic. Vercel
targets port 80 by default; the .NET base image and this app use 8080.

**Fix:** Set `PORT=8080` in the Vercel project (all environments) and redeploy.
Confirm the app logs `Now listening on: http://0.0.0.0:8080`. Never bind
`localhost` or `127.0.0.1`; the proxy cannot reach loopback inside the
container.

On other platforms, make the platform's target port match `PORT` (or let the
platform inject `PORT`, as Cloud Run, Render, and Railway do).

## Container exits with a permission error on port 80

**Cause:** The chiseled image runs as the non-root `app` user (UID 1654). Ports
below 1024 are privileged unless the host lowers
`net.ipv4.ip_unprivileged_port_start`.

**Fix:** Use port 8080 and tell the platform about it. Running as root is not a
portability fix.

## Nothing appears in the logs

**Cause:** A file, EventLog, or buffered logging provider, or an expectation that
`DOTNET_EnableDiagnostics=0` disables logging.

**Fix:** Keep console logging (`ClearProviders()` + `AddConsole()`). Vercel
collects only `stdout`/`stderr`. Diagnostics IPC (`dotnet-trace`,
`dotnet-counters`) is separate from logging; remove `DOTNET_EnableDiagnostics=0`
only when you need those tools.

## Logs from one request appear in another request

**Cause:** Vercel broadcasts container logs to all in-flight requests on the
instance; logs are not request-scoped.

**Fix:** Include a request or trace id in your own log messages, and do not rely
on per-request log isolation. ProblemDetails responses already include
`traceId`.

## Background work stops between requests

**Cause:** There is no process when there is no traffic. `BackgroundService`,
timers, and fire-and-forget tasks die when the instance scales in.

**Fix:** Move scheduled work to Vercel Cron or a durable workflow. Keep request
handlers idempotent and finish work before returning when it must complete.

## Uploads over about 4.5 MB return 413

**Cause:** The Vercel Function request/response limit is about 4.5 MB. Kestrel's
own `MaxRequestBodySize` is larger, so your app never sees the request.

**Fix:** Upload directly to object storage and send the URL/key to the API.
Raising Kestrel limits or using `[RequestSizeLimit]` does not change the platform
limit.

## Swagger returns 404

**Cause:** `Docs:Enabled` is `false`. The routes are not mapped when the flag is
off, so both `/swagger` and `/openapi/v1.json` return 404.

**Fix:** Set `Docs__Enabled=true` (or remove the override) and restart. Check
`GET /`; its `links` object lists `docs` and `openapi` only when docs are
enabled.

`/swagger` returning **301** is normal: Swagger UI redirects to
`/swagger/index.html`. Follow redirects with `curl -L`.

## Swagger UI shows the wrong server URL or scheme

**Cause:** The OpenAPI document contains a server URL derived from the request.
Behind a proxy, that request can carry the internal container host (for example
`127.0.0.1:32769` under `vercel dev`), so "Try it out" targets the wrong host.

**Fix:** The document transformer already replaces the server list with a single
relative URL, `/`, so Swagger UI resolves requests against the browser origin.
Keep that transformer when editing OpenAPI metadata. If you remove it, keep
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` and make sure `X-Forwarded-Host` and
`X-Forwarded-Proto` reach Kestrel; otherwise the document records the internal
address. Do not add HTTPS redirection; TLS already ends at the edge.

## `bad gateway` after changing the port

**Cause:** The platform still targets the old port.

**Fix:** Change the target port in the platform settings together with `PORT`.
On Vercel: project settings → Environment Variables → `PORT`; on Cloud Run:
`--port`; on Fly: `internal_port`; on Azure: `--target-port`.

## `vercel dev` cannot connect to Docker

**Cause:** `vercel dev` needs the Docker CLI and a running daemon to build and
run the container image.

**Fix:** Start Docker Desktop or Colima, verify with `docker info`, then rerun
`vercel dev -L`. `vercel dev` on port 3000 is the emulated edge; the app itself
still listens on `PORT` inside the container.

## `dotnet run` fails with "address already in use"

**Cause:** Another process already owns port 8080 (often a previous `dotnet run`
or a local container).

**Fix:** Stop it, or run on another port:

```bash
PORT=9999 dotnet run
```

## Validation does not reject invalid bodies

**Cause:** `AddValidation()` is missing from `AddApiServices`, or the request
type has no DataAnnotations attributes.

**Fix:** Keep `services.AddValidation()` and annotate the model. Invalid bodies
produce `400 application/problem+json` with an `errors` object. Malformed JSON
also returns 400 because `UseExceptionHandler` preserves
`BadHttpRequestException.StatusCode`.

## Image is larger than expected

**Cause:** The SDK, source, `.git`, or docs ended up in the runtime stage, or
satellite resources are included.

**Fix:** Keep the multi-stage build and `COPY --from=build /app/publish` only.
`dotnet publish` with `SatelliteResourceLanguages=en` and chiseled images keep
the image near 190 MB locally. Inspect with:

```bash
docker run --rm --entrypoint dotnet dotnet-serverless --info
docker history dotnet-serverless
```

## Chiseled image has no shell or curl

**Cause:** Chiseled images are distroless-style: no `/bin/sh`, no `curl`, no
package manager. That is why there is no Docker `HEALTHCHECK` command.

**Fix:** Use the platform's HTTP health check on `/health`. For interactive
debugging, temporarily run the non-chiseled `aspnet:10.0` image or a debug
sidecar; do not add a shell to the production image.
