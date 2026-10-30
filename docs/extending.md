# Extending the template

Everything below is intentionally absent. These are the insertion points, so a
feature can be added without moving the existing structure.

## Authentication and authorization

Add `Microsoft.AspNetCore.Authentication.JwtBearer`, configure the authority and
audience in `appsettings.json` (or environment variables), and call
`AddAuthentication().AddJwtBearer()` from `AddApiServices`. Add
`app.UseAuthentication()` and `app.UseAuthorization()` in `UseApiPipeline`
before the endpoints (authentication first, then authorization). Add
`RequireAuthorization()` to the endpoint group in `ExampleEndpoints`.

For Swagger, add a bearer security scheme in the OpenAPI document transformer or
with `AddSecurityRequirement`. Keep credentials out of `appsettings.json`; use
environment variables or a secret manager.

## Database

Use EF Core with a provider such as `Npgsql.EntityFrameworkCore.PostgreSQL`.
Register the `DbContext` in `AddApiServices`, read the connection string from
configuration, and keep the model in a `Data/` folder. On serverless hosts:

- Use the provider's connection pooling (for example Npgsql pooling) and keep
  the pool small; every instance is a separate process.
- Place the database in the same region as the functions.
- Do not run migrations at startup on every instance. Run them once in a
  separate job or deployment step.
- Remember there is no durable local filesystem; use object storage for files.

Vercel Marketplace Postgres integrations inject credentials as environment
variables. Use `vercel env pull` locally rather than committing secrets.

## Caching and rate limiting

Redis (Vercel Marketplace or another managed provider) is the usual cache.
Register `IDistributedCache` or a Redis client in `AddApiServices`. For rate
limiting, use ASP.NET Core's built-in rate limiter middleware in
`UseApiPipeline`; it is per instance, so distributed limits need Redis.

## File uploads and storage

Vercel rejects request bodies over about 4.5 MB before Kestrel sees them. Upload
directly from the client to object storage (for example Vercel Blob or S3) and
send the resulting URL or key to the API. Do not raise Kestrel's request-size
limit and expect Vercel to accept the request.

## Scheduled work and background jobs

The container exists only while requests are in flight. `BackgroundService`,
timers, and fire-and-forget tasks stop when the instance scales in. Options:

- **Vercel Cron** calls an HTTP endpoint on a schedule. Protect the endpoint with
  a shared secret and keep the work idempotent.
- **Vercel Workflow** or a durable queue handles long-running, multi-step work.
- **Daemons** are for containers that must run continuously, and are a separate
  Vercel product, not this app.

## Tests

The project is a single project so the SDK globs every `.cs` file. To add tests,
create a separate test project outside this directory (or in a `tests/` folder
and exclude it):

```xml
<ItemGroup>
  <Compile Remove="tests/**" />
</ItemGroup>
```

A separate test project is cleaner: it gets its own `.csproj`, can reference
`DotnetServerless.csproj`, and keeps test packages out of the runtime image.
`WebApplicationFactory<Program>` needs `public partial class Program { }` at the
end of `Program.cs` because top-level statements generate an internal class.

## CORS

Add `AddCors` in `AddApiServices` and `UseCors` in `UseApiPipeline` before
endpoint mapping. Configure explicit origins; avoid `AllowAnyOrigin` together
with credentials. For a browser-only API, keep the policy narrow.

## API versioning and OpenAPI

The `/api/v1` prefix is a plain route group. To add real versioning, add
`Asp.Versioning.Http`, configure it in `AddApiServices`, and map versioned
groups. The OpenAPI document transformer already sets the document title and
version; add security schemes there when authentication lands.

## Observability

Add OpenTelemetry (`OpenTelemetry.Extensions.Hosting`,
`OpenTelemetry.Instrumentation.AspNetCore`, and an exporter) in
`AddApiServices`. Log to stdout only, because that is all Vercel collects. If
you switch to structured JSON logging, keep the console provider and avoid
buffered writers so lines flush.

## Performance

- **ReadyToRun** reduces startup work but needs a fixed runtime identifier such
  as `linux-x64`, which conflicts with arm64 local builds. Add it in the Docker
  publish step, not in the project file.
- **Native AOT** produces a smaller, faster-starting image, but needs a
  `runtime-deps` chiseled base instead of the ASP.NET Core runtime image and
  restricts reflection-based features. Validate OpenAPI, validation, and JSON
  serialization first.
- Measure cold start after each change; image size is not the only factor.

## CI/CD

A minimal pipeline builds the image and runs a smoke test against `/health`.
GitHub Actions can build with Docker and push to Vercel Container Registry using
OIDC (see the Vercel docs for the VCR GitHub Action). Keep secrets in the CI
secret store, never in the repository.
