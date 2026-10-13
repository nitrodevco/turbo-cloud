using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Moderation;

/// <summary>The rooms a player entered lately.</summary>
[GenerateSerializer, Immutable]
public sealed record RoomVisitsEventMessageComposer : IComposer
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required string UserName { get; init; }

    [Id(2)]
    public required ImmutableArray<ModRoomVisitSnapshot> Rooms { get; init; }
}
