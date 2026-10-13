using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

/// <summary>
/// A word added in the Word Filter window. The client lists it only once the result says it was
/// added (<c>WordFilterSettingsView.onModifyCustomFilter</c>, result 1); a word that is refused is
/// answered nothing, so the list stays as it was.
/// </summary>
public class AddToCustomFilterMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<AddToCustomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        AddToCustomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var word = message.Word.Trim();
        var added = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .AddCustomFilterWordAsync(word, ct)
            .ConfigureAwait(false);

        if (!added)
            return;

        await ctx.SendComposerAsync(
                new ModifyCustomFilterResultMessageComposer
                {
                    Result = ModifyCustomFilterResultMessageComposer.RESULT_ADDED,
                    Word = word,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
