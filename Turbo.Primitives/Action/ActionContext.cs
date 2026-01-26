using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

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

    public static ActionContext Invalid =>
        new(ActionOrigin.System, SessionKey.Invalid, PlayerId.Invalid, RoomId.Invalid);
}
