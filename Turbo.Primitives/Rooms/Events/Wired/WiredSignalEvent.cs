using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Rooms.Events.Wired;

/// <summary>
/// A "send signal" action fired. <see cref="AntennaIds"/> are the furni the sender addressed;
/// every "receive signal" trigger whose own antenna selection meets them fires with the forwarded
/// furni and users as its signal payload.
/// </summary>
[GenerateSerializer]
public sealed record WiredSignalEvent : RoomEvent
{
    [Id(0)]
    public required HashSet<int> AntennaIds { get; init; }

    [Id(1)]
    public required HashSet<int> FurniIds { get; init; }

    [Id(2)]
    public required HashSet<RoomObjectId> AvatarIds { get; init; }

    [Id(3)]
    public required int Depth { get; init; }

    [Id(4)]
    public RoomObjectId SenderId { get; init; }

    /// <summary>
    /// The sending stack's triggerer, who triggers the receiving stack too (official client
    /// capture 2026-10-24/25, W4 lab selector.10.R4 4/4 and trigger.17.R2 3/3: a receiver's
    /// "triggering user" was the user, or the bot, that fired the sender).
    /// </summary>
    [Id(5)]
    public HashSet<RoomObjectId> TriggererIds { get; init; } = [];
}
