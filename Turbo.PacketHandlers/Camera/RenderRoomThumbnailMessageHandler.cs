using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// The room thumbnail camera's capture: the render data is kept for the room the player is in, and
/// <c>ThumbnailStatusMessage</c> answers (ok, or the day's limit hit).
/// </summary>
public class RenderRoomThumbnailMessageHandler(
    CameraPhotoStore store,
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

        var json = CameraPhotoStore.Inflate(message.Data);

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

        await store.SaveThumbnailAsync(ctx.RoomId.Value, json, ct).ConfigureAwait(false);
        await ctx.SendComposerAsync(
                new ThumbnailStatusMessageComposer { IsOk = true, IsRenderLimitHit = false },
                ct
            )
            .ConfigureAwait(false);
    }
}
