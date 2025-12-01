using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Snapshots.Avatars;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record UsersMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<RoomAvatarSnapshot> Avatars { get; init; }
}
