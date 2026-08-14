using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

[GenerateSerializer, Immutable]
public sealed record SpecialRoomEffectMessageComposer : IComposer
{
    [Id(0)]
    public required int EffectId { get; init; }
}
