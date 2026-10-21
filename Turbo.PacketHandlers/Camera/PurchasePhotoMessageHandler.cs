using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Camera;
using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.PacketHandlers.Camera;

/// <summary>
/// "Buy photo poster furni": the player's last photo becomes a photo poster in their inventory for
/// the configured price, then <c>CameraPurchaseOKMessage</c>. The poster's state names the photo
/// (<see cref="CameraPhotoPoster"/>); the photo's extra data, which the client asks the extra data
/// service for, is kept beside its picture before the poster is made.
/// </summary>
public class PurchasePhotoMessageHandler(
    IGrainFactory grainFactory,
    CameraPhotoStore store,
    IFurnitureDefinitionProvider definitions,
    IOptions<CameraConfig> config,
    ILogger<PurchasePhotoMessageHandler> logger
) : IMessageHandler<PurchasePhotoMessage>
{
    private readonly CameraConfig _config = config.Value;

    public async ValueTask HandleAsync(
        PurchasePhotoMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var photo = store.GetLastPhoto(ctx.PlayerId.Value);
        var definition = definitions.TryGetDefinitionByName(_config.PhotoFurnitureName);

        if (photo is null || definition is null)
        {
            if (definition is null)
                logger.LogError(
                    "No furniture definition {Name} for camera photos",
                    _config.PhotoFurnitureName
                );

            return;
        }

        var creator = await grainFactory
            .GetPlayerGrain(ctx.PlayerId)
            .GetSummaryAsync(ct)
            .ConfigureAwait(false);

        var debits = new List<WalletDebitRequest>();

        if (_config.CreditPrice > 0)
            debits.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.Credits,
                    Amount = _config.CreditPrice,
                }
            );

        if (_config.DucketPrice > 0)
            debits.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.ActivityPoints(ActivityPointTypeDuckets),
                    Amount = _config.DucketPrice,
                }
            );

        var cause = MovementCause.New(MovementCauses.CAMERA_PURCHASE, ctx.PlayerId.Value, photo.Id);

        if (debits.Count > 0)
        {
            var result = await grainFactory
                .GetPlayerWalletGrain(ctx.PlayerId)
                .TryDebitAsync(debits, cause, ct)
                .ConfigureAwait(false);

            if (!result.Succeeded)
                return;
        }

        await store
            .SaveExtraDataAsync(
                photo,
                CameraPhotoPoster.ExtraDataOf(photo, ctx.PlayerId.Value, creator.Name),
                ct
            )
            .ConfigureAwait(false);

        var state = CameraPhotoPoster.StateOf(photo, DateTime.UtcNow);
        var extraData = JsonSerializer.Serialize(
            new Dictionary<string, object> { [ExtraDataSectionType.STUFF] = new { Data = state } }
        );

        try
        {
            if (
                await grainFactory
                    .GetInventoryGrain(ctx.PlayerId)
                    .GrantFurnitureAsync(
                        definition.Id,
                        extraData,
                        cause.StartingAt(debits.Count),
                        ct
                    )
                    .ConfigureAwait(false)
                is null
            )
                throw new InvalidOperationException("the photo poster was not granted");
        }
        catch
        {
            await grainFactory
                .RefundAsync(ctx.PlayerId, debits, cause, logger, $"camera photo {photo.Id}")
                .ConfigureAwait(false);

            throw;
        }

        await ctx.SendComposerAsync(new CameraPurchaseOKMessageComposer(), ct)
            .ConfigureAwait(false);
    }

    private const int ActivityPointTypeDuckets = 0;
}
