using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

/// <summary>
/// A word removed in the Word Filter window; the client takes it off its list on result 3
/// (<c>WordFilterSettingsView.onModifyCustomFilter</c>).
/// </summary>
public class RemoveFromCustomFilterMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<RemoveFromCustomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        RemoveFromCustomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var removed = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .RemoveCustomFilterWordAsync(message.Word, ct)
            .ConfigureAwait(false);

        if (!removed)
            return;

        await ctx.SendComposerAsync(
                new ModifyCustomFilterResultMessageComposer
                {
                    Result = ModifyCustomFilterResultMessageComposer.RESULT_REMOVED,
                    Word = message.Word,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
