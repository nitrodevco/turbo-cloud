using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Commands.Snapshots;

/// <summary>One command a player may use, as <c>chat.commands</c> describes it.</summary>
[GenerateSerializer, Immutable]
public sealed record CommandTreeEntrySnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    /// <summary>Declared and hotel aliases, one list.</summary>
    [Id(1)]
    public required ImmutableArray<string> Aliases { get; init; }

    [Id(2)]
    public required string Category { get; init; }

    [Id(3)]
    public required string Description { get; init; }

    [Id(4)]
    public required string Usage { get; init; }

    /// <summary>The <c>RoomControllerType</c> the command needs; -1 for none.</summary>
    [Id(5)]
    public required int RoomLevel { get; init; }

    /// <summary>Runs outside the room, so the room level never applies.</summary>
    [Id(6)]
    public required bool Operator { get; init; }

    [Id(7)]
    public required ImmutableArray<CommandTreeParameterSnapshot> Parameters { get; init; }

    [Id(8)]
    public ImmutableArray<CommandSyntaxSnapshot> Syntax { get; init; } = [];
}
