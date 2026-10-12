using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record SpecialRoomEffectMessageComposer : IComposer
{
    [Id(0)]
    public required SpecialRoomEffectType Effect { get; init; }
}
