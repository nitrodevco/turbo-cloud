using System.Collections.Immutable;
using Turbo.Primitives.Commands.Enums;

namespace Turbo.Primitives.Commands;

/// <summary>
/// One parameter of a command, as its binder read it from the arguments record at registration.
/// </summary>
public sealed record CommandParameterInfo
{
    /// <summary>The name as the usage line spells it, lowercase.</summary>
    public required string Name { get; init; }

    public required CommandParameterKind Kind { get; init; }

    public required bool Optional { get; init; }

    public string Description { get; init; } = string.Empty;
    public string? Minimum { get; init; }
    public string? Maximum { get; init; }
    public int MinLength { get; init; } = -1;
    public int MaxLength { get; init; } = -1;
    public string DefaultValue { get; init; } = string.Empty;
    public bool Sensitive { get; init; }

    /// <summary>An enumeration's members, lowercase, in declaration order; empty otherwise.</summary>
    public ImmutableArray<string> Members { get; init; } = [];

    /// <summary>The <see cref="SuggestAttribute"/> source of a word; null for none.</summary>
    public string? SuggestionSource { get; init; }

    /// <summary>The <see cref="SelectorsAttribute"/> node of a player target; null for none.</summary>
    public string? SelectorNode { get; init; }

    /// <summary>Where a client finds the values to offer.</summary>
    public CommandSuggestType Suggest =>
        Kind switch
        {
            CommandParameterKind.Boolean
            or CommandParameterKind.Enumeration
            or CommandParameterKind.RoomPlayer
            or CommandParameterKind.Duration => CommandSuggestType.Client,
            CommandParameterKind.Player => CommandSuggestType.Server,
            CommandParameterKind.Word when SuggestionSource is not null =>
                CommandSuggestType.Server,
            _ => CommandSuggestType.None,
        };
}
