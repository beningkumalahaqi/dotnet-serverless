# Add an endpoint

This walks through adding a new feature group with
`POST /api/v1/text/uppercase`. A new group is the default choice when the route
does not belong to an existing feature: it keeps `SystemEndpoints` and
`ExampleEndpoints` small and gives the feature its own file, tag, and Swagger
section.

At a glance:

1. Add request/response records under `Models/`.
2. Add `Endpoints/TextEndpoints.cs` with the route group and handler.
3. Register the group with one line in `EndpointRouteBuilderExtensions.cs`.
4. Run the app, test with curl, and check Swagger.

You do not edit `Program.cs`. It calls `app.MapApiEndpoints()`, which is where
groups are registered.

## 1. Choose the route, method, and contract

| Decision | This example | Notes |
|----------|--------------|-------|
| Group file | `Endpoints/TextEndpoints.cs` | One file per feature. |
| Group prefix | `/api/v1/text` | The version prefix stays in the group. |
| Route | `/uppercase` | The full route is the group prefix plus this segment. |
| Method | `POST` | Use `GET` for reads, `POST` to create or to send a body, `PUT`/`PATCH` to update, `DELETE` to remove. |
| Request | JSON body `{ "text": "hello" }` | A complex type parameter is read from the body. |
| Response | `{ "text": "HELLO", "length": 5 }` | Return a record so the shape is stable and appears in OpenAPI. |
| Failure | `400` ProblemDetails | Comes from DataAnnotations validation, already wired up. |

The full route is `POST /api/v1/text/uppercase`.

## 2. Add the request model

Create `Models/UppercaseRequest.cs`:

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

Use properties with `{ get; init; }`, as in `EchoRequest`. Validation attributes
on properties are picked up by `AddValidation()`; you do not write manual checks.

## 3. Add the response model

Create `Models/UppercaseResponse.cs`:

```csharp
namespace DotnetServerless.Models;

public sealed record UppercaseResponse(string Text, int Length);
```

## 4. Create the route group

Create `Endpoints/TextEndpoints.cs`:

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

`MapGroup` builds the prefix once and returns a builder for the routes inside it.
`WithTags("Text")` puts the operation under **Text** in Swagger instead of
mixing it into another section.

## 5. Register the group

Open `Endpoints/EndpointRouteBuilderExtensions.cs` and add one line:

```csharp
public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
{
    app.MapSystemEndpoints();
    app.MapExampleEndpoints();
    app.MapTextEndpoints();

    return app;
}
```

That line is the only change outside `Models/` and the new endpoint file. If you
forget it, the route is not mapped and returns `404` even though the code
compiles.

## 6. Run it

```bash
dotnet run
```

Valid request:

```bash
curl -X POST http://localhost:8080/api/v1/text/uppercase \
  -H 'Content-Type: application/json' \
  -d '{"text":"hello"}'
```

Expected:

```json
{"text":"HELLO","length":5}
```

Invalid request (empty text):

```bash
curl -i -X POST http://localhost:8080/api/v1/text/uppercase \
  -H 'Content-Type: application/json' \
  -d '{"text":""}'
```

Expected: `HTTP/1.1 400 Bad Request` with
`Content-Type: application/problem+json` and an `errors` object.

## 7. Check that it is documented

Open <http://localhost:8080/swagger>. The operation is under **Text** with the
summary and the 200/400 responses you declared. The raw document is at
<http://localhost:8080/openapi/v1.json>.

No extra registration is needed. ASP.NET Core builds the OpenAPI document from
the route, the request/response types, and the metadata calls above.

## What each part does

| Code | Purpose |
|------|---------|
| `MapGroup("/api/v1/text")` | Creates the prefix and returns a route builder. |
| `MapPost("/uppercase", handler)` | Registers the route. `MapGet`, `MapPut`, `MapPatch`, and `MapDelete` work the same way. |
| `(UppercaseRequest request)` | A complex parameter is bound from the JSON body. Add `[FromBody]` only if you need to be explicit. |
| `string name` in a route with `{name}` | Simple types bind from the route. Without a matching route segment, they bind from the query string. |
| `string? lang` | Optional query parameter. A non-nullable value without a default is required. |
| `int id` with `/{id:int}` | Route constraint. A non-numeric value returns `404` before the handler runs. |
| `IGreetingService greetings` | Registered service, injected by type. No attribute is needed. |
| `CancellationToken cancellationToken` | Passed through automatically; forward it to async I/O. |
| `TypedResults.Ok(value)` | Returns `200` with `application/json`. |
| `TypedResults.Created(uri, value)` | Returns `201` with a `Location` header. |
| `TypedResults.NotFound()` | Returns `404` with ProblemDetails, via `UseStatusCodePages`. |
| `TypedResults.ValidationProblem(errors)` | Returns `400` with an `errors` object for manual validation. |
| `WithName("Uppercase")` | Unique operation id; also used by link generation. Keep it unique across the app. |
| `WithSummary` / `WithDescription` | Text shown in Swagger UI and stored in OpenAPI. |
| `WithTags("Text")` | Swagger section. Set it on the group so every route inherits it. |
| `Produces<T>()` | Declares the 200 response schema. |
| `ProducesValidationProblem()` | Declares the 400 validation response schema. |
| `ProducesProblem(StatusCodes.Status500InternalServerError)` | Declares an error response when the handler can return one. |

## Common variations

All snippets below stay inside the `TextEndpoints` group, so their full paths
start with `/api/v1/text`.

**Query parameters with defaults**

```csharp
text.MapGet("/search", (string? q, int page = 1) =>
        TypedResults.Ok(new { q, page }))
    .WithName("SearchText");
```

The variations use anonymous types to keep the snippets short. In a real
endpoint, define records so the schema is named and stable.

**Async handler**

```csharp
text.MapGet("/slow", async (CancellationToken cancellationToken) =>
{
    await Task.Delay(100, cancellationToken);
    return TypedResults.Ok(new HealthResponse("ok"));
});
```

**Header or explicit source**

```csharp
text.MapGet("/whoami", ([FromHeader(Name = "X-Client")] string? client) =>
        TypedResults.Ok(new { client }));
```

Add `using Microsoft.AspNetCore.Mvc;` for `[FromHeader]`. The same pattern works
for `[FromQuery]`, `[FromRoute]`, and `[FromBody]`.

**Expected 404 instead of an exception**

```csharp
text.MapGet("/maybe/{id:int}", IResult (int id) =>
        id <= 0
            ? TypedResults.NotFound()
            : TypedResults.Ok(new HelloResponse("found", "en")))
    .WithName("MaybeText")
    .Produces<HelloResponse>()
    .ProducesProblem(StatusCodes.Status404NotFound);
```

Use `TypedResults` for expected outcomes. Throw only for unexpected failures;
`UseExceptionHandler` turns those into `500` ProblemDetails.

## Extract logic into a service

Keep the handler small once logic grows. The template's `IGreetingService` is the
pattern.

`Services/IUppercaseService.cs`:

```csharp
using DotnetServerless.Models;

namespace DotnetServerless.Services;

public interface IUppercaseService
{
    UppercaseResponse Convert(UppercaseRequest request);
}
```

`Services/UppercaseService.cs`:

```csharp
using DotnetServerless.Models;

namespace DotnetServerless.Services;

public sealed class UppercaseService : IUppercaseService
{
    public UppercaseResponse Convert(UppercaseRequest request) =>
        new(request.Text.ToUpperInvariant(), request.Text.Length);
}
```

Register it in `Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.AddScoped<IUppercaseService, UppercaseService>();
```

Then replace the handler in `TextEndpoints.cs` with (add
`using DotnetServerless.Services;` to that file):

```csharp
text.MapPost("/uppercase", (UppercaseRequest request, IUppercaseService service) =>
        TypedResults.Ok(service.Convert(request)))
    .WithName("Uppercase")
    .WithSummary("Convert text to uppercase")
    .WithDescription("Validates the JSON body with DataAnnotations. Invalid input returns 400 ProblemDetails.")
    .Produces<UppercaseResponse>()
    .ProducesValidationProblem();
```

Use `AddScoped` for services that depend on a request or a `DbContext`, and
`AddSingleton` for stateless services. `GreetingService` is registered as a
singleton because it holds only a static lookup table.

Configuration belongs in `Options/` and is bound in `AddApiServices`; see
[configuration.md](configuration.md).

## When to use an existing group

A new feature gets its own group. If you are extending an existing feature, add
the same `MapPost` call to that group's file, using that group's builder
variable, instead of creating a new group. Do not create a group just to hold
one route that already belongs elsewhere.

## Checklist

- [ ] New group has its own file under `Endpoints/`
- [ ] Group is registered in `MapApiEndpoints`
- [ ] Full path includes the group prefix and does not collide
- [ ] `WithName` is unique across the app
- [ ] Request model uses properties with validation attributes
- [ ] Response is a named record, not an anonymous type
- [ ] Every status code the handler can return has `Produces*` metadata
- [ ] The group has a `WithTags` value, so Swagger stays organized
- [ ] Valid request returns the expected body
- [ ] Invalid request returns `400 application/problem+json`
- [ ] Swagger shows the operation under the new tag
- [ ] `/openapi/v1.json` contains the path
- [ ] No auth, database, or other unrequested feature slipped in

For features that are intentionally absent, such as authentication and a
database, see [extending.md](extending.md).
