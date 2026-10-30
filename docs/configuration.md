# Configuration

## Sources and precedence

Configuration is read from these sources, later sources winning:

1. `appsettings.json`
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json`
3. Environment variables
4. Command-line arguments

`:` is the section separator in JSON; use `__` in environment variables.
`Docs:Enabled` therefore becomes `Docs__Enabled`.

## Settings

### `PORT`

- **Environment variable:** `PORT`
- **Default:** `8080`
- **Used by:** `ConfigureServerlessHost` → `UseUrls("http://0.0.0.0:$PORT")`

The app always binds `0.0.0.0`, never `localhost` or `127.0.0.1`, because the
platform proxy connects from outside the container's loopback interface. An
invalid or out-of-range value falls back to `8080`.

On Vercel, set `PORT=8080` in project settings. Vercel routes container traffic
to port 80 by default, and a non-root container cannot bind 80. Do not "fix"
this by running the container as root; set the port instead.

### `Docs:Enabled`

- **Environment variable:** `Docs__Enabled`
- **Default:** `true`
- **Used by:** `AddApiServices` (options) and `UseApiPipeline`

When `true`, the app maps:

- `GET /openapi/v1.json` — OpenAPI 3.1 document
- `GET /swagger` — Swagger UI

When `false`, neither route is mapped and both return 404. The root endpoint
also omits the `docs` and `openapi` links.

The default is `true` so a freshly deployed template can be explored. For a real
API, disable it unless the API is intentionally public documentation:

```bash
vercel env add Docs__Enabled   # value: false
```

### `ASPNETCORE_ENVIRONMENT`

- **Default:** `Production` (the images do not set it)
- **Local development:** `Development` via `Properties/launchSettings.json`

This selects `appsettings.Development.json`, which raises ASP.NET Core log
verbosity. It does not enable the developer exception page in this template;
error responses stay ProblemDetails in every environment.

### `ASPNETCORE_FORWARDEDHEADERS_ENABLED`

- **Default in the image:** `true`
- **Default outside the image:** unset

When `true`, ASP.NET Core adds forwarded-headers middleware that trusts any
proxy. That is correct behind a platform edge such as Vercel, Cloud Run, Render,
or Azure Container Apps. If you expose Kestrel directly to the internet, unset
it, or configure `KnownProxies`/`KnownNetworks` and remove the env var.

`UseApiPipeline` also calls `UseForwardedHeaders` with the default options,
which trust only loopback. That keeps local development correct without
trusting arbitrary clients.

### Docker-only environment variables

These are set in the `Dockerfile`, not read by application code:

| Variable | Value | Purpose |
|----------|-------|---------|
| `DOTNET_EnableDiagnostics` | `0` | Disables the diagnostics IPC server. Remove it to use `dotnet-trace` / `dotnet-counters`. |
| `DOTNET_gcServer` | `0` | Workstation GC, better for small containers. |

## Per-platform examples

Vercel:

```bash
vercel env add PORT                 # 8080
vercel env add Docs__Enabled        # false (optional)
```

Cloud Run (PORT is injected automatically, default 8080):

```bash
gcloud run deploy dotnet-serverless \
  --source . \
  --region us-central1 \
  --allow-unauthenticated
```

Render injects `PORT` with a default of 10000. The app reads it, so no manual
port setting is needed. Set the health check path to `/health`.

Fly.io maps its proxy to an internal port in `fly.toml`. Keep `internal_port =
8080`, matching the container default:

```toml
[http_service]
  internal_port = 8080
  force_https = true
  auto_stop_machines = "stop"
  auto_start_machines = true
```

Railway injects `PORT`. Azure Container Apps uses ingress target port `8080`
without injecting `PORT`. See [deploy-other-platforms.md](deploy-other-platforms.md).
