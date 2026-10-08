using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.Registry;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;

namespace Turbo.PacketHandlers.Catalog;

/// <summary>
/// Sent when the catalog opens. Answers with the boxes, colours and ribbons the gift dialog may
/// offer and what a paid wrapping costs; the client keeps it for every gift bought this session.
/// </summary>
public class GetGiftWrappingConfigurationMessageHandler(IGiftWrappingProvider giftWrappingProvider)
    : IMessageHandler<GetGiftWrappingConfigurationMessage>
{
    private readonly IGiftWrappingProvider _giftWrappingProvider = giftWrappingProvider;

    public async ValueTask HandleAsync(
        GetGiftWrappingConfigurationMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await ctx.SendComposerAsync(
                new GiftWrappingConfigurationEventMessageComposer
                {
                    Wrapping = _giftWrappingProvider.GetWrapping(),
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
