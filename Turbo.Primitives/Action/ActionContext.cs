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
    public static ActionContext System { get; } =
        new(ActionOrigin.System, SessionKey.Invalid, PlayerId.Invalid, RoomId.Invalid);

    public static ActionContext Wired { get; } =
        new(ActionOrigin.Wired, SessionKey.Invalid, PlayerId.Invalid, RoomId.Invalid);

    public static ActionContext CreateForSystem(RoomId roomId) =>
        new(ActionOrigin.System, SessionKey.Invalid, PlayerId.Invalid, roomId);

    public static ActionContext CreateForWired(RoomId roomId) =>
        new(ActionOrigin.Wired, SessionKey.Invalid, PlayerId.Invalid, roomId);

    public static ActionContext CreateForPlayer(PlayerId playerId, RoomId roomId) =>
        new(ActionOrigin.Player, SessionKey.Invalid, playerId, roomId);

    public static ActionContext CreateForBot(RoomId roomId) =>
        new(ActionOrigin.Bot, SessionKey.Invalid, PlayerId.Invalid, roomId);

    public static ActionContext CreateForObjectContext(IRoomObjectContext ctx) =>
        ctx switch
        {
            IRoomPlayerContext playerCtx => CreateForPlayer(
                playerCtx.RoomObject.PlayerId,
                ctx.RoomId
            ),
            // A bot or pet acts on its own: no player is behind it, and it holds no rights.
            IRoomBotContext or IRoomPetContext => CreateForBot(ctx.RoomId),
            _ => throw new InvalidActionContextException(ctx.GetType()),
        };

    public static ActionContext Invalid =>
        new(ActionOrigin.System, SessionKey.Invalid, PlayerId.Invalid, RoomId.Invalid);
}
