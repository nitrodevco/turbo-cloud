using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Action;

[GenerateSerializer, Immutable]
public sealed record ExpressionMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required AvatarExpressionType ExpressionType { get; init; }
}
