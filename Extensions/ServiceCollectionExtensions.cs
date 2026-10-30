using DotnetServerless.Options;
using DotnetServerless.Services;
using Microsoft.OpenApi;

namespace DotnetServerless.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddProblemDetails();
        services.AddValidation();

        services.Configure<DocsOptions>(configuration.GetSection(DocsOptions.SectionName));

        services.AddOpenApi("v1", options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = "Dotnet Serverless API";
                document.Info.Version = "v1";
                document.Info.Description = "Minimal ASP.NET Core API ready to run as a serverless container.";
                document.Info.Contact = new OpenApiContact
                {
                    Name = "dotnet-serverless maintainers"
                };

                // Use a relative server URL so Swagger works behind any proxy. Without
                // this, .NET records the internal container address in the document.
                document.Servers ??= new List<OpenApiServer>();
                document.Servers.Clear();
                document.Servers.Add(new OpenApiServer { Url = "/" });

                return Task.CompletedTask;
            });
        });

        services.AddSingleton<IGreetingService, GreetingService>();

        return services;
    }
}
