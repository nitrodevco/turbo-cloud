using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

public class SetNewNavigatorWindowPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetNewNavigatorWindowPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetNewNavigatorWindowPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetNavigatorWindowPreferencesAsync(
                message.X,
                message.Y,
                message.Width,
                message.Height,
                message.OpenSavedSearches,
                message.ResultsMode,
                ct
            )
            .ConfigureAwait(false);
    }
}
