using System.ComponentModel.DataAnnotations;

namespace DotnetServerless.Models;

public sealed record EchoRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;

    [Range(1, 5)]
    public int Priority { get; init; } = 3;
}
