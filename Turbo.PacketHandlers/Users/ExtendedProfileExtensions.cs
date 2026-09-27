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
    /// Answers a profile request, by id or by name. The profile is the player grain's, its
    /// badge figures are the inventory's, its groups are the player's guild grain's and its
    /// friends are the messenger's; they are read here, side by side, because the player grain
    /// must not await the inventory or the messenger (both await it).
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
        var guildsTask = grainFactory.GetPlayerGuildGrain(playerId).GetMembershipsAsync(ct);
        var friendsTask = grainFactory
            .GetPlayerMessengerGrain(playerId)
            .GetProfileRelationAsync(ctx.PlayerId, ct);

        await Task.WhenAll(profileTask, badgesTask, guildsTask, friendsTask).ConfigureAwait(false);

        var profile = await profileTask.ConfigureAwait(false);
        var badges = await badgesTask.ConfigureAwait(false);
        var guilds = await guildsTask.ConfigureAwait(false);
        var friends = await friendsTask.ConfigureAwait(false);

        await ctx.SendComposerAsync(
                new ExtendedProfileMessageComposer
                {
                    Profile = profile with
                    {
                        Guilds = [.. guilds],
                        FriendCount = friends.FriendCount,
                        IsFriend = friends.IsFriend,
                        IsFriendRequestSent = friends.IsFriendRequestSent,
                    },
                    Badges = badges,
                },
                ct
            )
            .ConfigureAwait(false);
    }
}
