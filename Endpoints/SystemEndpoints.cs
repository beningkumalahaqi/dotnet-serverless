using System.Reflection;
using DotnetServerless.Models;
using DotnetServerless.Options;
using Microsoft.Extensions.Options;

namespace DotnetServerless.Endpoints;

public static class SystemEndpoints
{
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", (IHostEnvironment environment, IOptions<DocsOptions> docs) =>
            {
                var links = new Dictionary<string, string>
                {
                    ["health"] = "/health"
                };

                if (docs.Value.Enabled)
                {
                    links["docs"] = "/swagger";
                    links["openapi"] = "/openapi/v1.json";
                }

                return TypedResults.Ok(new ServiceInfoResponse(
                    "dotnet-serverless",
                    Version,
                    environment.EnvironmentName,
                    links));
            })
            .WithName("GetServiceInfo")
            .WithSummary("Service metadata")
            .WithDescription("Returns the service name, version, environment, and useful links.")
            .WithTags("System")
            .Produces<ServiceInfoResponse>();

        app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")))
            .WithName("GetHealth")
            .WithSummary("Liveness probe")
            .WithDescription("Returns 200 while the application is running. Point platform health checks here.")
            .WithTags("System")
            .Produces<HealthResponse>();

        return app;
    }

    private static string Version =>
        typeof(SystemEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion
            .Split('+')[0] ?? "unknown";
}
