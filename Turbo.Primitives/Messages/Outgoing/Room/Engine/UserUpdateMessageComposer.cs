using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Abstractions;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record UserUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomAvatarSnapshot> Avatars { get; init; }
}
