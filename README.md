# dotnet-serverless

A minimal ASP.NET Core 10 HTTP API packaged as a portable container. It runs on
[Vercel Container Runtime](https://vercel.com/docs/functions/runtimes/container)
and on any Docker/OCI host, and it includes OpenAPI plus Swagger UI out of the box.

**Live demo:** <https://dotnet-serverless.vercel.app/>

Nothing here is Vercel-specific except `vercel.json` and `docs/deploy-vercel.md`.
The app follows a plain container contract: listen on `0.0.0.0:$PORT`, log to
stdout, handle `SIGTERM`, keep no local state, and expose `GET /health`.

## Prerequisites

| Tool | Needed for | Install |
|------|------------|---------|
| .NET 10 SDK | `dotnet run` | <https://dotnet.microsoft.com/download/dotnet/10.0> |
| Docker | Building and running the image | <https://docs.docker.com/get-docker/> |
| Vercel CLI | `vercel dev`, `vercel deploy` | `npm i -g vercel` |

You can skip the .NET SDK if you only use Docker, and skip Docker if you only
use `dotnet run`.

## Quick start

### Run with the .NET SDK

```bash
dotnet run
```

Open <http://localhost:8080/swagger>. The port is read from `PORT` and defaults
to `8080`; `PORT=9999 dotnet run` listens on 9999.

### Run with Docker

```bash
docker build -t dotnet-serverless .
docker run --rm -p 8080:8080 dotnet-serverless
```

Open <http://localhost:8080/swagger>. The image is a multi-stage build on
`aspnet:10.0-noble-chiseled`, runs as the non-root `app` user (UID 1654), and
contains only the published app plus the ASP.NET Core runtime.

### Emulate Vercel locally

`vercel dev` needs the Docker CLI and a running Docker daemon.

```bash
vercel dev --local
```

Open <http://localhost:3000/swagger>. `--local` runs without linking a Vercel
project or authenticating. (`-L` is the deprecated alias.)

## Try it

```bash
curl http://localhost:8080/health
curl "http://localhost:8080/api/v1/examples/hello/Ada?lang=es"
curl -X POST http://localhost:8080/api/v1/examples/echo \
  -H 'Content-Type: application/json' \
  -d '{"message":"hello","priority":2}'
```

`examples/requests.http` contains the same calls for the VS Code REST Client,
Visual Studio, or JetBrains HTTP client. `examples/curl.sh` runs the whole set,
including the deliberately invalid requests:

```bash
cd examples
./curl.sh
```

## Add your own endpoint

[docs/add-endpoint.md](docs/add-endpoint.md) walks through adding a new route
group, from the request model and handler to validation, Swagger metadata, and a
curl test, plus when to extract logic into a service.

## Deploy to Vercel

One-time project setup, then deploy:

```bash
vercel login
vercel link
vercel env add PORT      # enter 8080, select all environments
vercel deploy --prod
```

The `PORT=8080` project variable is required. Vercel routes container traffic to
port 80 by default, and a non-root container cannot bind port 80. Binding to
`8080` and telling Vercel about it is the portable fix.

For Git-based deploys, push the repository and import it in Vercel. Every branch
gets a preview deployment. `vercel.json` sets `"framework": "container"` so the
build does not depend on framework auto-detection.

Full walkthrough, limits, and troubleshooting: [docs/deploy-vercel.md](docs/deploy-vercel.md).

## Deploy elsewhere

| Platform | Entry point |
|----------|-------------|
| Google Cloud Run | `gcloud run deploy --source .` (uses the Dockerfile) |
| Fly.io | `fly launch`, then `fly deploy` |
| Render | New Web Service, Docker runtime, health check `/health` |
| Railway | Deploy the repository; Dockerfile is detected |
| Azure Container Apps | `az containerapp up --source . --target-port 8080` |
| AWS App Runner / ECS | Push to ECR, set container port 8080 |

Details and caveats: [docs/deploy-other-platforms.md](docs/deploy-other-platforms.md).

## Project structure

```text
.
├── DotnetServerless.csproj   # the only project; assembly name is App
├── Program.cs                # composition root
├── Endpoints/                # route mapping, grouped by concern
├── Models/                   # request/response records
├── Services/                 # DI example (IGreetingService)
├── Options/                  # strongly typed configuration
├── Extensions/               # host, service, and pipeline wiring
├── Properties/               # local launch profile (port 8080)
├── docs/                     # topic guides
├── examples/                 # .http and curl request sets
├── Dockerfile                # multi-stage, chiseled, non-root
└── vercel.json               # pins the container framework
```

## Configuration

| Setting | Environment variable | Default | Notes |
|---------|----------------------|---------|-------|
| `PORT` | `PORT` | `8080` | Kestrel binds `0.0.0.0:$PORT`. Set `PORT=8080` in Vercel. |
| `Docs:Enabled` | `Docs__Enabled` | `true` | Serves `/swagger` and `/openapi/v1.json`. Set `false` to disable both. |
| — | `ASPNETCORE_ENVIRONMENT` | `Production` in the image | Use `Development` locally. |
| — | `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true` in the image | Trusts the platform proxy for scheme/client IP. |

Defaults live in `appsettings.json`. Environment variables override file values;
`appsettings.Development.json` overrides `appsettings.json` when the environment
is `Development`. See [docs/configuration.md](docs/configuration.md).

**Swagger is enabled by default so the deployed template is explorable.** Turn it
off once the API serves real data: `vercel env add Docs__Enabled` with value
`false`, or set `Docs__Enabled=false` on any host. Both `/swagger` and
`/openapi/v1.json` then return 404.

## Serverless caveats

- **No durable local state.** Containers scale to zero and are replaced. Use a
  database, object storage, or cache for anything that must survive a request.
- **No background work.** `BackgroundService`, timers, and fire-and-forget tasks
  stop when the instance scales in. Use a platform scheduler (for example Vercel
  Cron) that calls an HTTP endpoint.
- **4.5 MB request/response limit on Vercel.** Larger uploads get a 413 before
  the request reaches Kestrel. Upload files directly to object storage.
- **Cold starts.** A scaled-to-zero container pays image pull and process start
  on the first request. Measure your own numbers after deploying.
- **Vercel Container Runtime is in Beta.** Limits, pricing, and behavior can
  change. The app itself is platform-neutral; see
  [docs/deploy-other-platforms.md](docs/deploy-other-platforms.md).
- **Swagger is public by default.** Disable it with `Docs__Enabled=false` when
  that is not appropriate.

## What is intentionally not included

Authentication, database/ORM, caching, queues, background jobs, CORS, rate
limiting, CI/CD, tests, telemetry, and a multi-project solution are left out on
purpose. [docs/extending.md](docs/extending.md) explains where each one plugs in
without restructuring the app.

## Documentation

- [Add an endpoint](docs/add-endpoint.md)
- [Architecture](docs/architecture.md)
- [Configuration](docs/configuration.md)
- [Deploy to Vercel](docs/deploy-vercel.md)
- [Deploy to other platforms](docs/deploy-other-platforms.md)
- [Extending the template](docs/extending.md)
- [Troubleshooting](docs/troubleshooting.md)

## License

MIT. See [LICENSE](LICENSE).
