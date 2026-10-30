namespace DotnetServerless.Models;

public sealed record ServiceInfoResponse(
    string Name,
    string Version,
    string Environment,
    IReadOnlyDictionary<string, string> Links);

public sealed record HealthResponse(string Status);
