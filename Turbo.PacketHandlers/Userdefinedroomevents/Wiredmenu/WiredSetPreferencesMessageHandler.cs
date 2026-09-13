using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Userdefinedroomevents.Wiredmenu;

public class WiredSetPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<WiredSetPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        WiredSetPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetWiredPreferencesAsync(
                message.WiredMenuButton,
                message.WiredInspectButton,
                message.WiredPlayTestMode,
                message.VariableSyntaxMode,
                message.WiredWhisperDisabled,
                message.ShowAllNotifications,
                message.UIStyle,
                ct
            )
            .ConfigureAwait(false);
    }
}
