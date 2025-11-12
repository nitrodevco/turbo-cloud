using Orleans;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Layout;

[GenerateSerializer, Immutable]
public sealed record RoomOccupiedTilesMessageComposer : IComposer
{
    // TODO: add properties if/when identified
}
