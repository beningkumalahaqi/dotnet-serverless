using System.Diagnostics;
using System.Runtime.InteropServices;
using DotnetServerless.Models;
using DotnetServerless.Services;

namespace DotnetServerless.Endpoints;

public static class ExampleEndpoints
{
    public static IEndpointRouteBuilder MapExampleEndpoints(this IEndpointRouteBuilder app)
    {
        var examples = app.MapGroup("/api/v1/examples").WithTags("Examples");

        examples.MapGet("/hello/{name}", (string name, string? lang, IGreetingService greetings) =>
                TypedResults.Ok(greetings.Greet(name, lang)))
            .WithName("SayHello")
            .WithSummary("Return a greeting")
            .WithDescription("Demonstrates route and query binding plus a service resolved from DI.")
            .Produces<HelloResponse>();

        examples.MapPost("/echo", (EchoRequest request) =>
                TypedResults.Ok(new EchoResponse(
                    request.Message,
                    request.Priority,
                    request.Message.Length,
                    DateTimeOffset.UtcNow)))
            .WithName("Echo")
            .WithSummary("Echo a validated JSON body")
            .WithDescription("The request body is validated with DataAnnotations before the handler runs. An invalid body returns 400 ProblemDetails.")
            .Produces<EchoResponse>()
            .ProducesValidationProblem();

        examples.MapGet("/runtime", (IHostEnvironment environment) =>
            {
                var process = Process.GetCurrentProcess();
                return TypedResults.Ok(new RuntimeInfoResponse(
                    RuntimeInformation.FrameworkDescription,
                    RuntimeInformation.OSDescription,
                    environment.EnvironmentName,
                    Environment.MachineName,
                    Environment.ProcessId,
                    Math.Round((DateTimeOffset.UtcNow - process.StartTime.ToUniversalTime()).TotalSeconds, 1)));
            })
            .WithName("GetRuntimeInfo")
            .WithSummary("Inspect the current instance")
            .WithDescription("Shows that serverless instances are ephemeral: the instance id and uptime can differ between requests.")
            .Produces<RuntimeInfoResponse>();

        examples.MapGet("/error", IResult () =>
                throw new InvalidOperationException("This exception was thrown on purpose to demonstrate error handling."))
            .WithName("ThrowError")
            .WithSummary("Return a 500 error")
            .WithDescription("Throws on purpose. In Production the response is a ProblemDetails body without a stack trace.")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return app;
    }
}
