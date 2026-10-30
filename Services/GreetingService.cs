using DotnetServerless.Models;

namespace DotnetServerless.Services;

public sealed class GreetingService : IGreetingService
{
    private static readonly Dictionary<string, string> Templates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "Hello, {0}!",
        ["es"] = "¡Hola, {0}!"
    };

    public HelloResponse Greet(string name, string? language)
    {
        var resolved = language is not null && Templates.ContainsKey(language)
            ? language.ToLowerInvariant()
            : "en";

        return new HelloResponse(string.Format(Templates[resolved], name), resolved);
    }
}
