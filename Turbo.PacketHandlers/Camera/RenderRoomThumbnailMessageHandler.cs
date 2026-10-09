using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The room thumbnail camera's capture: the render data is kept for the room the player is in and
/// drawn (<see cref="CameraRenderer"/>), and <c>ThumbnailStatusMessage</c> answers (ok, or the
/// day's limit hit). Only a player who may edit the room's settings (owner level, as the room
/// grain's own settings check) gets one: the client shows the button only to them
/// (<c>RoomInfoViewCtrl</c>: <c>add_thumbnail_region</c> visible when <c>canEditRoomSettings</c>).
/// </summary>
public class RenderRoomThumbnailMessageHandler(
    CameraPhotoStore store,
    CameraRenderer renderer,
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
        if (ctx.PlayerId <= 0 || ctx.RoomId.Value <= 0)
            return;

        var level = await grainFactory
            .GetRoomGrain(ctx.RoomId)
            .GetControllerLevelAsync(ctx.PlayerId, ct)
            .ConfigureAwait(false);
        var json =
            level >= RoomControllerType.Owner ? CameraPhotoStore.Inflate(message.Data) : null;

        if (json is null)
        {
            await ctx.SendComposerAsync(
                    new ThumbnailStatusMessageComposer { IsOk = false, IsRenderLimitHit = false },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        if (!store.TryCount(ctx.PlayerId.Value, "thumbnail", _config.ThumbnailLimitPerDay))
        {
            await ctx.SendComposerAsync(
                    new ThumbnailStatusMessageComposer { IsOk = false, IsRenderLimitHit = true },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        var (jsonPath, pngPath) = await store
            .SaveThumbnailAsync(ctx.RoomId.Value, json, ct)
            .ConfigureAwait(false);

        // Best effort, as the photo's render: the data is stored either way and the PNG logged if not drawn.
        await renderer.RenderAsync(jsonPath, pngPath, ct).ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new ThumbnailStatusMessageComposer { IsOk = true, IsRenderLimitHit = false },
                ct
            )
            .ConfigureAwait(false);
    }
}
