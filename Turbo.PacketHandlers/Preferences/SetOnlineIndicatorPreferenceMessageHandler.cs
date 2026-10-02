using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

/// <summary>
/// Saves who the player is told about coming online (<c>OtherSettingsView</c>'s
/// "friend online" drop menu), which comes back in <c>AccountPreferences</c> at login.
/// </summary>
public class SetOnlineIndicatorPreferenceMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetOnlineIndicatorPreferenceMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetOnlineIndicatorPreferenceMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetOnlineIndicatorPreferenceAsync(message.Selection, ct)
            .ConfigureAwait(false);
    }
}
