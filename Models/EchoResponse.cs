namespace DotnetServerless.Models;

public sealed record EchoResponse(string Message, int Priority, int Length, DateTimeOffset ReceivedAt);
