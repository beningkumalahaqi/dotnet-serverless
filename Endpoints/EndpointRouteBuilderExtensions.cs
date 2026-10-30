namespace DotnetServerless.Endpoints;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapSystemEndpoints();
        app.MapExampleEndpoints();

        return app;
    }
}
