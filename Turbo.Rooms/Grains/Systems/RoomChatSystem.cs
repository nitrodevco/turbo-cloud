using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Rooms.Grains.Systems;

public sealed class RoomChatSystem(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public async Task SendChatAsync(
        RoomObjectId objectId,
        string text,
        AvatarGestureType gesture,
        int styleId,
        List<(string, string, bool)> links,
        int trackingId
    )
    {
        await _roomGrain.SendComposerToRoomAsync(
            new ChatMessageComposer
            {
                ObjectId = objectId,
                Text = text,
                Gesture = gesture,
                StyleId = styleId,
                Links = links,
                TrackingId = trackingId,
            }
        );
    }

    public async Task SendChatFromPlayerAsync(
        PlayerId playerId,
        string text,
        AvatarGestureType gesture,
        int styleId,
        List<(string, string, bool)> links,
        int trackingId
    )
    {
        if (
            !_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
        )
            return;

        await SendChatAsync(avatar.ObjectId, text, gesture, styleId, links, trackingId);
    }
}
