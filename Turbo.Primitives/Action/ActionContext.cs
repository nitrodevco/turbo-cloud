using System;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Primitives.Action;

[GenerateSerializer, Immutable]
public readonly record struct ActionContext(
    ActionOrigin Origin,
    SessionKey SessionKey,
    PlayerId PlayerId,
    RoomId RoomId
)
{
    public static ActionContext CreateForSystem(RoomId roomId) =>
        new(ActionOrigin.System, SessionKey.Invalid, PlayerId.Invalid, roomId);

    public static ActionContext CreateForWired(RoomId roomId) =>
        new(ActionOrigin.Wired, SessionKey.Invalid, PlayerId.Invalid, roomId);

    public static ActionContext CreateForPlayer(PlayerId playerId, RoomId roomId) =>
        new(ActionOrigin.Player, SessionKey.Invalid, playerId, roomId);

    public static ActionContext CreateForObjectContext(IRoomObjectContext ctx) =>
        ctx switch
        {
            IRoomPlayerContext playerCtx => CreateForPlayer(
                playerCtx.RoomObject.PlayerId,
                ctx.RoomId
            ),
            _ => throw new Exception("Cannot create ActionContext for object context"),
        };

    public static ActionContext Invalid =>
        new(ActionOrigin.System, SessionKey.Invalid, PlayerId.Invalid, RoomId.Invalid);
}
