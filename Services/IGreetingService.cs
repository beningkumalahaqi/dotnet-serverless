using DotnetServerless.Models;

namespace DotnetServerless.Services;

public interface IGreetingService
{
    HelloResponse Greet(string name, string? language);
}
