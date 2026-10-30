using DotnetServerless.Options;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace DotnetServerless.Extensions;

public static class WebApplicationExtensions
{
    private const int DefaultPort = 8080;

    public static WebApplicationBuilder ConfigureServerlessHost(this WebApplicationBuilder builder)
    {
        var port = ResolvePort(builder.Configuration["PORT"]);
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

        // Vercel sends SIGTERM and allows 30 seconds before the container is killed.
        builder.Services.Configure<HostOptions>(options =>
            options.ShutdownTimeout = TimeSpan.FromSeconds(25));

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        return builder;
    }

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // The container sets ASPNETCORE_FORWARDEDHEADERS_ENABLED=true, which trusts the
        // platform proxy. These defaults only trust loopback, so local dev stays safe.
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
                ? badRequest.StatusCode
                : StatusCodes.Status500InternalServerError
        });
        app.UseStatusCodePages();

        var docs = app.Services.GetRequiredService<IOptions<DocsOptions>>().Value;
        if (docs.Enabled)
        {
            app.MapOpenApi("/openapi/{documentName}.json");
            app.UseSwaggerUI(options =>
            {
                options.RoutePrefix = "swagger";
                options.DocumentTitle = "Dotnet Serverless API";
                options.SwaggerEndpoint("/openapi/v1.json", "Dotnet Serverless API v1");
                options.DisplayRequestDuration();
            });
        }

        return app;
    }

    private static int ResolvePort(string? configured)
    {
        return int.TryParse(configured, out var port) && port is > 0 and <= 65535
            ? port
            : DefaultPort;
    }
}
