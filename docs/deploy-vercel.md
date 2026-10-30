# Deploy to Vercel

Vercel does not have a first-party .NET runtime. ASP.NET Core is a self-hosted
Kestrel server, so it runs on Vercel as a **container** on Vercel Functions.

Container Runtime is **Beta** and available on all plans. The app is deliberately
platform-neutral, so moving to another container host is a configuration change,
not a rewrite. See [deploy-other-platforms.md](deploy-other-platforms.md).

## How it works

1. Vercel detects the root `Dockerfile` (or `Containerfile`) and selects the
   **Container** preset. Our `vercel.json` pins `"framework": "container"` so a
   previously linked framework cannot win.
2. Vercel builds the image for **linux/amd64**, pushes it to the Vercel Container
   Registry (VCR), and routes all project traffic to it.
3. The image runs as a Vercel Function on Fluid compute: it scales with traffic,
   shares an instance across concurrent requests, and scales to zero when idle.
   CPU billing pauses while the code waits on I/O.
4. The function starts the container and waits for an HTTP server on `PORT`.

A root `Dockerfile` is enough on a brand-new project; `vercel.json` is included
for determinism. `Dockerfile.vercel` or `Containerfile.vercel` would take
precedence over a detected framework if you ever need it.

## One-time setup

```bash
vercel login
vercel link                 # create or link the project
vercel env add PORT         # value: 8080, select all environments
```
`PORT=8080` is required. Vercel routes to port **80** by default, and the
chiseled image runs as a non-root user that cannot bind 80. The app binds
`0.0.0.0:$PORT`, so telling Vercel `8080` is the portable fix. Do not run the
container as root to use port 80.

Optional, once the API has real data:

```bash
vercel env add Docs__Enabled   # value: false
```

This makes `/swagger` and `/openapi/v1.json` return 404.

## Deploy

Preview deployment:

```bash
vercel deploy
```

Production deployment:

```bash
vercel deploy --prod
```

For Git-based deployments, push the repository, import it in Vercel, and keep
the project root at the repository root. Every branch push and pull request gets
a preview URL; the production branch deploys to production.

## Verify

```bash
BASE_URL=https://your-deployment.vercel.app

curl "$BASE_URL/health"
curl "$BASE_URL/api/v1/examples/runtime"
curl -s -o /dev/null -w '%{http_code}\n' "$BASE_URL/swagger"
curl "$BASE_URL/openapi/v1.json" | head -c 200
```

`/api/v1/examples/runtime` is the quickest way to confirm the container is live:
it reports the .NET version, Ubuntu version, machine name, and process uptime.
The machine name and uptime change as Vercel replaces instances.

## Local emulation

```bash
vercel dev --local
```

`--local` runs without linking or authenticating. (`-L` still works but is
deprecated.) Vercel dev needs the Docker CLI and a running Docker daemon. The URL
is usually <http://localhost:3000>. This path builds the same image and applies
the same `PORT` routing as a deployment, which makes it the best pre-deploy
check.

## Port, scaling, and shutdown

| Behavior | Value |
|----------|-------|
| Default target port | 80 |
| Override | `PORT` project environment variable |
| Scale-in, production | 5 minutes without traffic |
| Scale-in, preview | 30 seconds without traffic |
| Shutdown signal | `SIGTERM` |
| Grace period | 30 seconds |
| App shutdown timeout | 25 seconds (`HostOptions.ShutdownTimeout`) |

The app exits well inside the grace period. `docker stop` measured **0.12 s** on
a local arm64 image (2026-10-30).

## Logs and limits

- **Logs:** only `stdout` and `stderr` are collected. They are not tied to a
  request and are broadcast to all in-flight requests on the instance. The image
  configures console logging and no file sinks. `DOTNET_EnableDiagnostics=0`
  disables the diagnostics IPC server, not logging.
- **Request/response body limit:** about **4.5 MB**. A larger upload is rejected
  with 413 before Kestrel sees it. Upload files directly to object storage and
  pass the URL to the API.
- **No persistent filesystem.** Do not store durable state in the container.
- **No background work.** `BackgroundService`, timers, and fire-and-forget tasks
  stop when the instance scales in. Use Vercel Cron to call an HTTP endpoint, or
  a durable workflow for long-running work.
- **Function limits apply.** See [Vercel Functions limits](https://vercel.com/docs/functions/limitations).
- **Secure Compute and Static IPs** are not supported with custom container
  images.

## Measured numbers

Measured in this repository on 2026-10-30, on Apple Silicon:

| Measurement | Value |
|-------------|-------|
| Local image size (`linux/arm64`) | 188 MB |
| Local container start to first successful `/health` | under 1 s |
| Local graceful shutdown (`docker stop`) | 0.12 s |

Vercel builds `linux/amd64`, so its image size can differ. Vercel cold-start
time depends on image size, region, and platform state; measure it after your
first deploy instead of assuming these local numbers carry over.

## Advanced: push to VCR yourself

`vercel deploy` builds and pushes automatically. To build and push an image
explicitly, for example from CI:

```bash
vercel link
vercel vcr login docker
vercel vcr build docker . --push
```

The default reference is
`vcr.vercel.com/<team-slug>/<project-name>/<project-name>:latest`. `vercel vcr
build` targets `linux/amd64` by default, which is what Vercel Functions run. See
[Vercel Container Registry](https://vercel.com/docs/container-registry).

## Troubleshooting

Start with [troubleshooting.md](troubleshooting.md). The most common first-deploy
failure is a **502 because `PORT` was not set to 8080**, or the app was changed
to bind `localhost` instead of `0.0.0.0`.
