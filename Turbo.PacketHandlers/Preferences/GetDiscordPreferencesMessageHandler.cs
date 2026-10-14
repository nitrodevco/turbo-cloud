using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Incoming.Preferences;
using Turbo.Primitives.Messages.Outgoing.Preferences;
using Turbo.Primitives.Orleans;

namespace Turbo.PacketHandlers.Preferences;

/// <summary>
/// The client asks once at start (<c>DiscordSettingsController.initComponent</c>) and Habbo
/// answers with what the player saved, or version 0 and four falses when they never did
/// (official client capture: a new account is answered 0000000000000000; after a save,
/// 0000000101010101 on the next login).
/// </summary>
public class GetDiscordPreferencesMessageHandler(IGrainFactory grainFactory)
    : IMessageHandler<GetDiscordPreferencesMessage>
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async ValueTask HandleAsync(
        GetDiscordPreferencesMessage message,
        MessageContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.PlayerId <= 0)
            return;

        var settings = await _grainFactory
            .GetPlayerSettingsGrain(ctx.PlayerId)
            .GetSettingsAsync(ct)
            .ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new DiscordPreferencesMessageComposer
                {
                    Version = settings.DiscordPreferencesVersion,
                    ShowHabbo = settings.DiscordShowHabbo,
                    ShareActivity = settings.DiscordShareActivity,
                    HideInHiddenRooms = settings.DiscordHideInHiddenRooms,
                    AllowJoining = settings.DiscordAllowJoining,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
