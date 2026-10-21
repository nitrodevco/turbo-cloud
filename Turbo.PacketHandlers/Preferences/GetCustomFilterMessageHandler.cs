using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

/// <summary>
/// The toolbar's Word Filter window asks for the player's own word filter as it opens
/// (<c>WordFilterSettingsView.prepareWindow</c>); Habbo answers with the words (an empty list
/// recorded on hhus, 2026-10-13).
/// </summary>
public class GetCustomFilterMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetCustomFilterMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetCustomFilterMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        var words = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .GetCustomFilterWordsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(new GetCustomFilterResultMessageComposer { Words = words }, ct)
            .ConfigureAwait(false);
    }
}
