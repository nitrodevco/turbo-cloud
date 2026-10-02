using System;
using System.Collections.Generic;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Commands;

/// <summary>
/// Everything core knows about a registered command, built once when it is registered. A plugin
/// reads its own attributes through <see cref="Type"/> in <c>CommandExecutingEvent</c> to veto.
/// </summary>
public sealed class CommandDescriptor
{
    public required string Name { get; init; }

    /// <summary>Aliases declared in code, never including <see cref="Name"/>.</summary>
    public required IReadOnlyList<string> Aliases { get; init; }

    public required string Description { get; init; }

    /// <summary>The section <c>:commands</c> lists it under.</summary>
    public required string Category { get; init; }

    /// <summary>Holding any one of these lets the executor run it.</summary>
    public required IReadOnlyList<string> Nodes { get; init; }

    /// <summary>None when the command works outside a room's authority.</summary>
    public required RoomControllerType? MinimumRoomLevel { get; init; }

    public required Type Type { get; init; }

    public required ICommand Command { get; init; }

    public required ICommandBinder Binder { get; init; }

    /// <summary>The command's own fallback texts, by status; a hotel's texts win.</summary>
    public required IReadOnlyDictionary<string, string> Texts { get; init; }

    /// <summary>Acts on the hotel, outside the room's turn, and may run from the console.</summary>
    public bool IsOperator => Command is IOperatorCommand;

    public string Usage => Binder.Usage;

    /// <summary>The node that lets an executor use <c>@room</c> and <c>@online</c>; null for none.</summary>
    public string? SelectorNode => Binder.SelectorNode;
}
