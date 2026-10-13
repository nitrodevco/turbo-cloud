using System;

namespace Turbo.Primitives.Commands;

/// <summary>Validation and help shared by the binder, completion and generated help.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class CommandParameterAttribute : Attribute
{
    public string Description { get; init; } = string.Empty;
    public string? Minimum { get; init; }
    public string? Maximum { get; init; }
    public int MinLength { get; init; } = -1;
    public int MaxLength { get; init; } = -1;
    public bool Sensitive { get; init; }

    /// <summary>
    /// The word must be written in double quotes. For a free text that sits before optional
    /// words, so that where it ends is never a guess: <c>:gift bob throne false true</c> is
    /// refused rather than sent with the note "false".
    /// </summary>
    public bool Quoted { get; init; }
}
