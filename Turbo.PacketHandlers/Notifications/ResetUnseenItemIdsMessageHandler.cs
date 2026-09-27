using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Messages.Incoming.Notifications;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Notifications;

/// <summary>The client saw some items of one inventory tab.</summary>
public class ResetUnseenItemIdsMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<ResetUnseenItemIdsMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        ResetUnseenItemIdsMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (
            ctx.PlayerId <= 0
            || !Enum.IsDefined((UnseenItemCategory)message.Category)
            || message.ItemIds.Count == 0
        )
            return;

        await _grainFactory
            .GetPlayerUnseenItemsGrain(ctx.PlayerId)
            .ResetItemsAsync((UnseenItemCategory)message.Category, [.. message.ItemIds], ct)
            .ConfigureAwait(false);
    }
}
