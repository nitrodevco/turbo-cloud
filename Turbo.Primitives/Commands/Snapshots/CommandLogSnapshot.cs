using System;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Commands.Snapshots;

/// <summary>A command use waiting to be written to <c>command_logs</c>.</summary>
[GenerateSerializer, Immutable]
public sealed record CommandLogSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required PlayerId PlayerId { get; init; }

    [Id(2)]
    public required string Command { get; init; }

    /// <summary>What the executor typed after the name, unfiltered.</summary>
    [Id(3)]
    public required string Arguments { get; init; }

    [Id(4)]
    public required CommandOutcome Outcome { get; init; }

    /// <summary>When it ran: the write happens later, so the row cannot say.</summary>
    [Id(5)]
    public required DateTime LoggedAtUtc { get; init; }
}
