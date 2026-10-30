namespace DotnetServerless.Models;

public sealed record RuntimeInfoResponse(
    string Framework,
    string Os,
    string Environment,
    string Instance,
    int ProcessId,
    double UptimeSeconds);
