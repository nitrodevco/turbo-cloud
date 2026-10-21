using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The room thumbnail camera's capture: the render data is kept for the room the player is in and
/// drawn (<see cref="ICameraRenderer"/>) as the room's thumbnail (<see cref="RoomThumbnailStore"/>), and <c>ThumbnailStatusMessage</c> answers (ok, or the
/// day's limit hit). Only a player who may edit the room's settings (owner level, as the room
/// grain's own settings check) gets one: the client shows the button only to them
/// (<c>RoomInfoViewCtrl</c>: <c>add_thumbnail_region</c> visible when <c>canEditRoomSettings</c>).
/// The capture runs off the packet (<see cref="CameraCaptureJobs"/>), so the render does not hold
/// up the player's session; one sent while their last capture is still being drawn is not ok.
/// </summary>
public class RenderRoomThumbnailMessageHandler(
    CameraPhotoStore store,
    RoomThumbnailStore thumbnails,
    ICameraRenderer renderer,
    CameraCaptureJobs jobs,
    IGrainFactory grainFactory,
    IOptions<CameraConfig> config
) : IMessageHandler<RenderRoomThumbnailMessage>
{
    private readonly CameraConfig _config = config.Value;

    public async ValueTask HandleAsync(
        RenderRoomThumbnailMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.RoomId.Value <= 0)
            return;

        var level = await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetControllerLevelAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);
        var playerId = ctx.PlayerId;
        var roomId = ctx.RoomId;

        if (
            level >= RoomControllerType.Owner
            && jobs.TryStart(playerId, token => CaptureAsync(message.Data, playerId, roomId, token))
        )
            return;

        await ctx.SendComposerAsync(
                new ThumbnailStatusMessageComposer { IsOk = false, IsRenderLimitHit = false },
                ct
            )
            .ConfigureAwait(false);
    }

    private async Task<IComposer> CaptureAsync(
        byte[] data,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        // Counted before it is inflated, so data that is not a render is refused within the limit too.
        var limitHit = !store.TryCount(playerId.Value, "thumbnail", _config.ThumbnailLimitPerDay);
        var json = limitHit ? null : store.Inflate(data);

        if (json is null)
            return new ThumbnailStatusMessageComposer { IsOk = false, IsRenderLimitHit = limitHit };

        var (jsonPath, pngPath) = await thumbnails
            .SaveAsync(roomId.Value, json, ct)
            .ConfigureAwait(false);

        // Best effort, as the photo's render: the data is stored either way and the PNG logged if not drawn.
        await renderer.RenderAsync(jsonPath, pngPath, ct).ConfigureAwait(false);

        return new ThumbnailStatusMessageComposer { IsOk = true, IsRenderLimitHit = false };
    }
}
