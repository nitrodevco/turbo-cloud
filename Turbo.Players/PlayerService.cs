using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Players;

/// <summary>
/// The profile packets' entry point (see <see cref="IPlayerService"/>). It holds no state: the
/// profile is the player grain's, its badge figures the badge grain's, its groups the player's
/// guild grain's, its friends the messenger's and the hidden switch the settings grain's. They
/// are read here side by side because the player grain must not await the badge grain or the
/// messenger (both await it).
/// </summary>
public sealed class PlayerService(IGrainFactory grainFactory) : IPlayerService
{
    private readonly IGrainFactory _grainFactory = grainFactory;

    public async Task<IComposer?> GetExtendedProfileAsync(
        PlayerId viewerId,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (playerId <= 0)
            return null;

        var profileTask = _grainFactory
            .GetPlayerGrain(playerId)
            .GetExtendedProfileSnapshotAsync(ct);
        var badgesTask = _grainFactory.GetPlayerBadgeGrain(playerId).GetBadgeSummaryAsync(ct);
        var guildsTask = _grainFactory.GetPlayerGuildGrain(playerId).GetMembershipsAsync(ct);
        var friendsTask = _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .GetProfileRelationAsync(viewerId, ct);
        var hiddenTask = IsHiddenFromAsync(viewerId, playerId, ct);

        await Task.WhenAll(profileTask, badgesTask, guildsTask, friendsTask, hiddenTask)
            .ConfigureAwait(false);

        var profile = await profileTask.ConfigureAwait(false);
        var friends = await friendsTask.ConfigureAwait(false);
        var hidden = await hiddenTask.ConfigureAwait(false);

        // ExtendedProfileWindowCtrl.refreshHeader shows "-" for a friend count or last login of
        // -1 and covers the groups with full_profile_hidden, so a hidden profile sends neither.
        // Whether the viewer is a friend, or has asked to be, is theirs to know either way.
        return new ExtendedProfileMessageComposer
        {
            Profile = profile with
            {
                Guilds = hidden ? [] : [.. await guildsTask.ConfigureAwait(false)],
                FriendCount = hidden ? -1 : friends.FriendCount,
                LastAccessSinceInSeconds = hidden ? -1 : profile.LastAccessSinceInSeconds,
                IsFriend = friends.IsFriend,
                IsFriendRequestSent = friends.IsFriendRequestSent,
                IsHidden = hidden,
            },
            Badges = await badgesTask.ConfigureAwait(false),
        };
    }

    public async Task<IComposer?> GetExtendedProfileByNameAsync(
        PlayerId viewerId,
        string playerName,
        CancellationToken ct
    )
    {
        var playerId = await _grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerIdAsync(playerName, ct)
            .ConfigureAwait(false);

        return playerId is { } id
            ? await GetExtendedProfileAsync(viewerId, id, ct).ConfigureAwait(false)
            : null;
    }

    public async Task<IComposer> GetRelationshipStatusInfoAsync(
        PlayerId viewerId,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        // An empty list draws every category as "no friends in this category", which is how a
        // hidden profile's relationships look on Habbo.
        var entries =
            playerId > 0 && !await IsHiddenFromAsync(viewerId, playerId, ct).ConfigureAwait(false)
                ? await _grainFactory
                    .GetPlayerMessengerGrain(playerId)
                    .GetRelationshipStatusInfoAsync(ct)
                    .ConfigureAwait(false)
                : [];

        return new RelationshipStatusInfoEventMessageComposer
        {
            UserId = playerId,
            Entries = entries,
        };
    }

    /// <summary>Whether <paramref name="playerId"/> hides their profile from this viewer; never from themselves.</summary>
    private async Task<bool> IsHiddenFromAsync(
        PlayerId viewerId,
        PlayerId playerId,
        CancellationToken ct
    ) =>
        viewerId != playerId
        && (
            await _grainFactory
                .GetPlayerSettingsGrain(playerId)
                .GetSettingsAsync(ct)
                .ConfigureAwait(false)
        ).ProfileHidden;
}
