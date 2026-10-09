using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// "Publish on web": the player's last photo is published for the configured duckets, at most one
/// per <see cref="CameraConfig.PublishCooldownSeconds"/>; <c>CameraPublishStatusMessage</c> answers
/// with the photo's id, or the seconds left to wait.
/// </summary>
public class PublishPhotoMessageHandler(
    IGrainFactory grainFactory,
    CameraPhotoStore store,
    IOptions<CameraConfig> config
) : IMessageHandler<PublishPhotoMessage>
{
    private readonly CameraConfig _config = config.Value;

    public async ValueTask HandleAsync(
        PublishPhotoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var photo = store.GetLastPhoto(ctx.PlayerId.Value);

        if (photo is null)
            return;

        var wait = store.SecondsUntilPublish(ctx.PlayerId.Value);

        if (wait > 0)
        {
            await ctx.SendComposerAsync(
                    new CameraPublishStatusMessageComposer { IsOk = false, SecondsToWait = wait },
                    ct
                )
                .ConfigureAwait(false);

            return;
        }

        if (_config.PublishDucketPrice > 0)
        {
            var result = await grainFactory
                .GetPlayerWalletGrain(ctx.PlayerId)
                .TryDebitAsync(
                    new List<WalletDebitRequest>
                    {
                        new()
                        {
                            CurrencyKind = CurrencyKind.ActivityPoints(0),
                            Amount = _config.PublishDucketPrice,
                        },
                    },
                    ct
                )
                .ConfigureAwait(false);

            if (!result.Succeeded)
                return;
        }

        store.MarkPublished(ctx.PlayerId.Value);

        await ctx.SendComposerAsync(
                new CameraPublishStatusMessageComposer
                {
                    IsOk = true,
                    SecondsToWait = 0,
                    ExtraDataId = photo.Id,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
