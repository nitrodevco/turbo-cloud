using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The photo lab's Preview: the render data is kept as the player's last photo, drawn into its PNG
/// (<see cref="ICameraRenderer"/>) and its url sent back (<c>CameraStorageUrlMessage</c>); an empty
/// url past the day's limit, or for data that is not a render, is the client's
/// <c>camera.render.count.info</c>. The render counts before it is inflated, so data that is not
/// a render is refused within the day's limit too. All of it runs off the packet
/// (<see cref="CameraCaptureJobs"/>), so the render does not hold up the player's session; a
/// photo sent while the last one is still being drawn gets the empty url, and is not counted.
/// </summary>
public class RenderRoomMessageHandler(
    CameraPhotoStore store,
    ICameraRenderer renderer,
    CameraCaptureJobs jobs,
    IOptions<CameraConfig> config
) : IMessageHandler<RenderRoomMessage>
{
    private readonly CameraConfig _config = config.Value;

    public async ValueTask HandleAsync(
        RenderRoomMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var playerId = ctx.PlayerId;
        var roomId = ctx.RoomId;

        if (jobs.TryStart(playerId, token => CaptureAsync(message.Data, playerId, roomId, token)))
            return;

        await ctx.SendComposerAsync(new CameraStorageUrlMessageComposer { Url = string.Empty }, ct)
            .ConfigureAwait(false);
    }

    private async Task<IComposer> CaptureAsync(
        byte[] data,
        PlayerId playerId,
        RoomId roomId,
        CancellationToken ct
    )
    {
        var url = string.Empty;

        if (
            store.TryCount(playerId.Value, "photo", _config.RenderLimitPerDay)
            && store.Inflate(data) is { } json
        )
        {
            var photo = await store
                .SavePhotoAsync(playerId.Value, roomId.Value, json, ct)
                .ConfigureAwait(false);

            // With its small copy: bought, the photo is a poster, and a poster draws that one.
            await renderer
                .RenderAsync(photo.JsonPath, photo.PngPath, ct, withSmall: true)
                .ConfigureAwait(false);
            url = photo.Url;
        }

        return new CameraStorageUrlMessageComposer { Url = url };
    }
}
