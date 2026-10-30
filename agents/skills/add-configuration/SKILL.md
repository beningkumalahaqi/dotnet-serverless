---
name: add-configuration
description: Add or change application configuration in dotnet-serverless. Use when introducing an options class, appsettings key, environment variable, or reading configuration from an endpoint or service. Covers Options/, binding in AddApiServices, appsettings.json, and configuration docs.
---

# Add configuration

Configuration is read through the .NET configuration system, bound to typed
options, and injected with `IOptions<T>`. The reference implementation is
`Options/DocsOptions.cs` plus its use in `Extensions/ServiceCollectionExtensions.cs`
and `Extensions/WebApplicationExtensions.cs`.

Do not read environment variables directly in handlers or services, and do not
put secrets in `appsettings.json`.

## 1. Create the options class

`Options/<Feature>Options.cs`

```csharp
namespace DotnetServerless.Options;

public sealed class UppercaseOptions
{
    public const string SectionName = "Uppercase";

    public bool Enabled { get; set; } = true;

    public int MaxLength { get; set; } = 200;
}
```

Rules:

- `sealed class`, not a record, matching `DocsOptions`.
- Expose `SectionName` as a `const` so binding and docs cannot drift.
- Give every setting a safe default; a missing key should not crash startup
  unless the setting is genuinely required.
- Keep secrets out. Credentials arrive through environment variables or a
  secret manager at deployment time.

## 2. Bind it in `AddApiServices`

Add one registration in `Extensions/ServiceCollectionExtensions.cs`:

```csharp
services.Configure<UppercaseOptions>(
    configuration.GetSection(UppercaseOptions.SectionName));
```

Put it next to the existing `DocsOptions` registration. Do not bind application
options in `Program.cs`.

If a setting is required, prefer failing at startup over failing per request:

```csharp
services.AddOptions<UppercaseOptions>()
    .Bind(configuration.GetSection(UppercaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

Use this only when the added complexity is justified; the default template uses
plain `Configure<T>`. The validation snippet needs
`using Microsoft.Extensions.Options;`, which `ServiceCollectionExtensions.cs`
does not currently import.

## 3. Add the non-secret default

`appsettings.json`

```json
{
  "Docs": {
    "Enabled": true
  },
  "Uppercase": {
    "Enabled": true,
    "MaxLength": 200
  }
}
```

- Keep `appsettings.json` production-safe and free of credentials.
- Use `appsettings.Development.json` only for local overrides.
- The environment variable form replaces `:` with `__`:
  `Uppercase:MaxLength` becomes `Uppercase__MaxLength`.

## 4. Consume it

Inject the options interface where it is needed:

```csharp
app.MapPost("/uppercase", (UppercaseRequest request,
        IOptions<UppercaseOptions> options) =>
    {
        var max = options.Value.MaxLength;
        return TypedResults.Ok(new UppercaseResponse(
            request.Text.ToUpperInvariant(),
            Math.Min(request.Text.Length, max)));
    })
    .WithName("Uppercase")
    .WithSummary("Convert text to uppercase")
    .Produces<UppercaseResponse>()
    .ProducesValidationProblem();
```

- `IOptions<T>` for settings read once at startup.
- `IOptionsMonitor<T>` if a value must react to reloads at runtime.
- Do not inject `IConfiguration` into handlers just to read one value.
- Add `using DotnetServerless.Options;` and
  `using Microsoft.Extensions.Options;` to the endpoint file; the Web SDK's
  implicit usings do not include the options namespace.

## 5. Document user-facing settings

If the setting affects deployment or operations:

1. Add a row to the configuration table in `README.md`.
2. Add a section to `docs/configuration.md` with the environment variable name,
   default, and effect.
3. Mention the `vercel env add <Name>` command when it is relevant to Vercel.

Keep internal-only knobs out of the public docs.

## 6. Verify

Run with an explicit override and confirm the behavior changes:

```bash
Uppercase__Enabled=false PORT=18080 dotnet run --no-build
```

Also confirm the default still works with no override. Check that `GET /` still
starts, and that the endpoint or service actually reads the option and reflects
the new value. Stop the server afterwards.

For a deployment, set the variable through the platform rather than committing
it:

```bash
vercel env add Uppercase__Enabled
```

## Do not

- Add a secret to `appsettings.json` or `appsettings.Development.json`.
- Hard-code a section name in two places; use `SectionName`.
- Read `Environment.GetEnvironmentVariable(...)` in a handler.
- Add a config section that nothing reads.
- Change `PORT` handling or bind options outside `AddApiServices` without a
  specific reason.

## Checklist

- [ ] `Options/<Feature>Options.cs` exists with `SectionName` and defaults
- [ ] Bound in `AddApiServices` off the matching section
- [ ] Non-secret default added to `appsettings.json`
- [ ] Consumed through `IOptions<T>` or `IOptionsMonitor<T>`
- [ ] Environment variable name uses `__`
- [ ] User-facing setting documented in README/docs
- [ ] No secrets committed
- [ ] Default and overridden behavior both verified
