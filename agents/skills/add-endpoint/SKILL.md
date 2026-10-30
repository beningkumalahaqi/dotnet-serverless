---
name: add-endpoint
description: Add or change HTTP endpoints in this ASP.NET Core 10 minimal API (dotnet-serverless). Use when the task involves a new route, request/response model, DataAnnotations validation, OpenAPI metadata, or moving handler logic into a service. Covers Endpoints/, Models/, Services/, and registration in MapApiEndpoints.
---

# Add an endpoint

This repository is one ASP.NET Core 10 project using minimal APIs. `Program.cs`
is the fixed composition root; routes are registered from
`Endpoints/EndpointRouteBuilderExtensions.cs`.

Do not introduce controllers, move routing into another project, or edit
`Program.cs` just to add a route. Background and rationale live in
`docs/add-endpoint.md`; this skill is the edit-and-verify procedure.

## 1. Choose the route group

- **Existing feature:** add the route to that feature's
  `Endpoints/<Feature>Endpoints.cs`, using its existing group builder.
- **New feature:** create `Endpoints/<Feature>Endpoints.cs` and map
  `app.MapGroup("/api/v1/<feature>").WithTags("<Feature>")`.
- Keep `/api/v1` in the group prefix, not on each route.
- Do not put a new production feature into `ExampleEndpoints` or
  `SystemEndpoints`.

Pick the method and contract before writing code:

| Need | Use |
|------|-----|
| Read data | `MapGet` |
| Create, or send a complex body | `MapPost` |
| Replace | `MapPut` |
| Partial update | `MapPatch` |
| Remove | `MapDelete` |

The full path is the group prefix plus the route segment, for example
`POST /api/v1/text/uppercase`.

## 2. Add the contracts under `Models/`

Request DTOs use property syntax so DataAnnotations attach to properties:

`Models/UppercaseRequest.cs`

```csharp
using System.ComponentModel.DataAnnotations;

namespace DotnetServerless.Models;

public sealed record UppercaseRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Text { get; init; } = string.Empty;
}
```

Response DTOs are named positional records:

`Models/UppercaseResponse.cs`

```csharp
namespace DotnetServerless.Models;

public sealed record UppercaseResponse(string Text, int Length);
```

Rules:

- One type per file as with `EchoRequest`/`EchoResponse`; the grouped
  `SystemResponses.cs` is the existing exception for tightly related system
  DTOs.
- Do not use anonymous types for committed responses; named records keep the
  OpenAPI schema stable.
- Do not write manual validation for things DataAnnotations can express.
  `AddValidation()` is already registered, so invalid bodies become
  `400 application/problem+json` with an `errors` object.
- Reuse an existing model when the shape already exists.

## 3. Create the route group

`Endpoints/TextEndpoints.cs`

```csharp
using DotnetServerless.Models;

namespace DotnetServerless.Endpoints;

public static class TextEndpoints
{
    public static IEndpointRouteBuilder MapTextEndpoints(this IEndpointRouteBuilder app)
    {
        var text = app.MapGroup("/api/v1/text").WithTags("Text");

        text.MapPost("/uppercase", (UppercaseRequest request) =>
                TypedResults.Ok(new UppercaseResponse(
                    request.Text.ToUpperInvariant(),
                    request.Text.Length)))
            .WithName("Uppercase")
            .WithSummary("Convert text to uppercase")
            .WithDescription("Validates the JSON body with DataAnnotations. Invalid input returns 400 ProblemDetails.")
            .Produces<UppercaseResponse>()
            .ProducesValidationProblem();

        return app;
    }
}
```

Binding rules:

| Parameter | Source |
|-----------|--------|
| Complex type | JSON body |
| Simple type with a matching `{name}` segment | Route |
| Simple type without a matching segment | Query string |
| Registered interface/class | DI |
| `CancellationToken` | Framework-provided; forward it to async I/O |

Use `[FromHeader]`, `[FromQuery]`, `[FromRoute]`, or `[FromBody]` only when
explicit binding is needed (`using Microsoft.AspNetCore.Mvc;`).

Metadata rules:

- `WithName(...)` must be unique across the app; it is the OpenAPI operation id.
- `WithTags(...)` belongs on the group so every route inherits it.
- `Produces<T>()` declares the success schema.
- `ProducesValidationProblem()` declares the 400 body for validated input.
- `ProducesProblem(StatusCodes.Status404NotFound)` (or another status) declares
  every expected error the handler can return.
- Use `TypedResults` for expected outcomes. Throw only for unexpected failures;
  the pipeline turns those into 500 ProblemDetails.

For async handlers, use `async`/`await`, accept `CancellationToken`, and never
block with `.Result` or `.Wait()`.

## 4. Register the group

Add one line in `Endpoints/EndpointRouteBuilderExtensions.cs`:

```csharp
public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
{
    app.MapSystemEndpoints();
    app.MapExampleEndpoints();
    app.MapTextEndpoints();

    return app;
}
```

If this line is missing, the code compiles but the route returns 404. Do not
register routes anywhere else.

## 5. Extract logic only when it earns a service

Create a service when the handler has real logic, does I/O, calls an external
system, or is reused. Keep trivial transformations inline.

```csharp
// Services/IUppercaseService.cs
using DotnetServerless.Models;

namespace DotnetServerless.Services;

public interface IUppercaseService
{
    UppercaseResponse Convert(UppercaseRequest request);
}
```

```csharp
// Services/UppercaseService.cs
using DotnetServerless.Models;

namespace DotnetServerless.Services;

public sealed class UppercaseService : IUppercaseService
{
    public UppercaseResponse Convert(UppercaseRequest request) =>
        new(request.Text.ToUpperInvariant(), request.Text.Length);
}
```

Register it in `Extensions/ServiceCollectionExtensions.cs`, inside
`AddApiServices`:

```csharp
services.AddScoped<IUppercaseService, UppercaseService>();
```

- `AddSingleton` for stateless services such as the existing
  `IGreetingService`.
- `AddScoped` for request-scoped state or a `DbContext`.
- Do not register application services in `Program.cs`.

## 6. Respect the serverless contract

These constraints are properties of the platform, not style preferences:

- **No durable in-memory state.** Instances scale to zero and run concurrently.
  Persist state externally.
- **No background work.** `BackgroundService`, timers, and fire-and-forget tasks
  stop when the instance scales in. Use a scheduler that calls an HTTP endpoint,
  or a durable workflow.
- **No durable local files.** Use object storage.
- **Vercel body limit is about 4.5 MB.** Do not raise Kestrel limits to work
  around it; upload directly to object storage.
- **Logs go to stdout/stderr only.** Include a request or trace id if you add
  request-specific logs.
- Do not add HTTPS redirection, change the `PORT` binding, or add auth, a
  database, CORS, or rate limiting unless the task asks for it.

## 7. Verify

Build and run on a non-default port to avoid colliding with an existing server:

```bash
dotnet build
PORT=18080 dotnet run --no-build
```

In another shell:

```bash
curl -i -X POST http://localhost:18080/api/v1/text/uppercase \
  -H 'Content-Type: application/json' \
  -d '{"text":"hello"}'

curl -i -X POST http://localhost:18080/api/v1/text/uppercase \
  -H 'Content-Type: application/json' \
  -d '{"text":""}'

curl -s http://localhost:18080/openapi/v1.json \
  | jq -e '.paths["/api/v1/text/uppercase"]'
```

Expect `200` with the response body, `400` with
`Content-Type: application/problem+json` and an `errors` object, and a non-null
OpenAPI path entry. Stop the server afterwards. For fuller checks (Docker,
Docs flag, error responses), use the `verify-api` skill.

If `dotnet` reports that SDK `10.0.100` is missing, install .NET 10 or use
Docker. Do not lower `global.json` to make the build pass.

## Do not

- Return anonymous types from committed endpoints.
- Duplicate an existing route or `WithName`.
- Add a new group for one route that belongs to an existing feature.
- Add unrequested auth, persistence, caching, or background infrastructure.
- Edit generated `bin/` or `obj/` files.

## Checklist

- [ ] Route lives in an existing group, or in a new `Endpoints/<Feature>Endpoints.cs`
- [ ] Group uses `/api/v1/<feature>` and a `WithTags` value
- [ ] Group is registered in `MapApiEndpoints`
- [ ] Request model uses properties with DataAnnotations
- [ ] Response is a named record
- [ ] Every possible status code has `Produces*` metadata
- [ ] `WithName` is unique
- [ ] Handler is thin; non-trivial logic is in a registered service
- [ ] Valid request returns the expected body
- [ ] Invalid request returns 400 ProblemDetails
- [ ] The route appears in `/openapi/v1.json` and under the expected Swagger tag
- [ ] No serverless constraint was violated
