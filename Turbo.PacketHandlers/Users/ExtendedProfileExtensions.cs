using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.PacketHandlers.Users;

internal static class ExtendedProfileExtensions
{
    /// <summary>
    /// Answers a profile request, by id or by name. The profile is the player grain's and its
    /// badge figures are the inventory's; they are read here, side by side, because the player
    /// grain must not await the inventory.
    /// </summary>
    public static async Task SendExtendedProfileAsync(
        this MessageContext ctx,
        IGrainFactory grainFactory,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var profileTask = grainFactory.GetPlayerGrain(playerId).GetExtendedProfileSnapshotAsync(ct);
        var badgesTask = grainFactory.GetInventoryGrain(playerId).GetBadgeSummaryAsync(ct);

        await Task.WhenAll(profileTask, badgesTask).ConfigureAwait(false);

        var profile = await profileTask.ConfigureAwait(false);
        var badges = await badgesTask.ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ExtendedProfileMessageComposer { Profile = profile, Badges = badges },
                ct
            )
            .ConfigureAwait(false);
    }
}
