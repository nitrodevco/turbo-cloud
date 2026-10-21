using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

/// <summary>
/// Keeps the Discord settings window's choices. Habbo sends nothing back (official client
/// capture: no reply to SetDiscordPreferences); the client already holds what it sent.
/// </summary>
public class SetDiscordPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<SetDiscordPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        SetDiscordPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .SetDiscordPreferencesAsync(
                message.Version,
                message.ShowHabbo,
                message.ShareActivity,
                message.HideInHiddenRooms,
                message.AllowJoining,
                ct
            )
            .ConfigureAwait(false);
    }
}
