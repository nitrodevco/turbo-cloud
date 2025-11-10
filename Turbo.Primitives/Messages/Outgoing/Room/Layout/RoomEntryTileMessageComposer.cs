using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Layout;

public sealed record RoomEntryTileMessageComposer : IComposer
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required Rotation Rotation { get; init; }
}
