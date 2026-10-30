# Deploy to other platforms

The image follows the same contract everywhere: listen on `0.0.0.0:$PORT`
(default `8080`), log to stdout, handle `SIGTERM`, stay stateless, and serve
`GET /health`. Nothing outside `docs/deploy-vercel.md` and `vercel.json` is
Vercel-specific.

Last verified against platform docs: **2026-10-30**.

## Port handling at a glance

| Platform | How it finds the port | What to set |
|----------|-----------------------|-------------|
| Vercel | `PORT` project environment variable; default 80 | `PORT=8080` |
| Google Cloud Run | Injects `PORT`; default 8080 | Nothing |
| Fly.io | `internal_port` in `fly.toml`; default 8080 | `internal_port = 8080` |
| Render | Injects `PORT`; default 10000 | Nothing (health check `/health`) |
| Railway | Injects `PORT` | Nothing |
| Azure Container Apps | Ingress target port | `--target-port 8080` |
| AWS App Runner | Service image port; default 8080 | Port `8080`, health check `/health` |
| AWS ECS / Fargate | Task definition + load balancer target | Container port `8080` |

All of these terminate TLS at their edge. The image sets
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, so the app sees the original scheme
and client IP. Do not add HTTPS redirection.

## Google Cloud Run

Cloud Run injects `PORT` and defaults it to `8080`, so no port configuration is
needed.

```bash
gcloud run deploy dotnet-serverless \
  --source . \
  --region us-central1 \
  --allow-unauthenticated
```

`--source .` builds the root `Dockerfile` with Cloud Build; it does not use the
.NET SDK on your machine. To deploy from an image instead:

```bash
gcloud builds submit --tag us-central1-docker.pkg.dev/PROJECT/REPO/dotnet-serverless
gcloud run deploy dotnet-serverless \
  --image us-central1-docker.pkg.dev/PROJECT/REPO/dotnet-serverless \
  --region us-central1 \
  --allow-unauthenticated
```

Notes: Cloud Run scales to zero by default and has a startup probe; `/health`
works for that. Set `--concurrency` deliberately: the app handles concurrent
requests, but a small memory limit still constrains it. See
[Cloud Run container contract](https://cloud.google.com/run/docs/container-contract).

## Fly.io

`fly launch` detects the Dockerfile and creates a `fly.toml`; `fly deploy` builds
and ships it.

```bash
fly launch --no-deploy
fly deploy
```

Keep the proxy pointed at `8080`, and make shutdown explicit:

```toml
[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = "stop"
  auto_start_machines = true
  min_machines_running = 0

kill_signal = "SIGTERM"
kill_timeout = 30

[[http_service.checks]]
  grace_period = "5s"
  interval = "30s"
  method = "GET"
  path = "/health"
  timeout = "5s"
```

Fly sends `SIGINT` by default, which .NET also handles, but setting `SIGTERM`
matches the container contract. Add `[[vm]]` sizing after measuring. See
[Fly app configuration](https://fly.io/docs/reference/configuration/).

## Render

Create a **Web Service** from the repository and choose the **Docker** runtime.
Render injects `PORT` (default `10000`) and forwards traffic to it; the app reads
that variable, so nothing needs to be hard-coded. Set the health check path to
`/health`.

The Dockerfile's `EXPOSE 8080` is documentation only; Render uses `PORT`.
Render terminates TLS at its edge and supports zero-downtime deploys. Free
instances sleep after inactivity, so the first request after idle is slow. See
[Render web services](https://render.com/docs/web-services#port-binding).

## Railway

Push the repository into a new Railway project. Railway detects the capital-D
`Dockerfile` at the root and builds it. Generate a public domain under
**Settings → Networking**.

Railway injects `PORT`, so the app binds the right port automatically. Railway's
default `RAILWAY_DEPLOYMENT_DRAINING_SECONDS` is `0`, which leaves no time between
`SIGTERM` and `SIGKILL`; set it to `30` to let the app drain:

```bash
railway variables --set RAILWAY_DEPLOYMENT_DRAINING_SECONDS=30
```

See [Railway Dockerfiles](https://docs.railway.com/builds/dockerfiles) and
[public networking](https://docs.railway.com/guides/public-networking).

## Azure Container Apps

Azure Container Apps does not inject `PORT`; the ingress target port must match
the container. The app defaults to `8080`, so:

```bash
az containerapp up \
  --name dotnet-serverless \
  --resource-group my-rg \
  --location centralus \
  --source . \
  --target-port 8080 \
  --ingress external
```

`--source .` builds with ACR Tasks and creates the environment if needed. HTTP
ingress adds `X-Forwarded-Proto` and `X-Forwarded-For`; the image trusts them.
Container Apps scales to zero when configured (`minReplicas: 0`) and terminates
TLS at ingress. See
[Azure Container Apps ingress](https://learn.microsoft.com/azure/container-apps/ingress-overview).

## AWS App Runner

App Runner runs `linux/amd64`. On an Apple Silicon machine, build for that
platform explicitly; otherwise `docker build` produces an arm64 image the
service cannot start:

```bash
docker build --platform linux/amd64 -t dotnet-serverless .
```

Emulation makes this slower than a native build. `vercel deploy`, Cloud Run
`--source .`, and Azure `--source .` build remotely, so they pick the right
architecture for you.

Push the image to ECR, then create an App Runner service from that image:

```bash
aws ecr create-repository --repository-name dotnet-serverless
aws ecr get-login-password --region us-east-1 \
  | docker login --username AWS --password-stdin ACCOUNT.dkr.ecr.us-east-1.amazonaws.com
docker tag dotnet-serverless:latest \
  ACCOUNT.dkr.ecr.us-east-1.amazonaws.com/dotnet-serverless:latest
docker push ACCOUNT.dkr.ecr.us-east-1.amazonaws.com/dotnet-serverless:latest
```

In the App Runner service settings, set the container port to `8080` (the
default) and the health check to HTTP `/health`. App Runner injects no `PORT`
variable; the image's default is already correct. See
[App Runner ports](https://docs.aws.amazon.com/apprunner/latest/dg/network-ports.html).

## AWS ECS / Fargate

Register a task definition with container port `8080`, attach it to a target
group on port `8080`, and put an ALB in front. No `PORT` variable is needed; the
image default applies. Use `/health` for the target group health check and set
the deregistration delay high enough for in-flight requests.

## AWS Lambda

Out of scope. Lambda does not run a listening HTTP server; it needs an adapter
such as the Lambda ASP.NET Core hosting package or a custom runtime. Use App
Runner, ECS, or another container host when you want this image unchanged.

## Common checklist

- [ ] Platform is configured to send traffic to port **8080**, or injects `PORT`
- [ ] Health check path is `/health`
- [ ] One instance can serve concurrent requests (Fluid compute, Cloud Run
      concurrency, Fly concurrency, and so on)
- [ ] No local filesystem state; external storage is configured
- [ ] `Docs__Enabled=false` if Swagger should not be public
- [ ] Graceful shutdown window is at least 25 seconds
- [ ] Memory is sized for .NET, not for a static site (start at 512 MB and measure)
