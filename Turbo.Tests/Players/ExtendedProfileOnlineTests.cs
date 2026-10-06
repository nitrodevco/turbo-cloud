using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Orleans;
using Turbo.Players;
using Turbo.Primitives.Badges.Snapshots;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Messenger;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The large profile's online mark: read from the presence grain that owns the session, as the
/// messenger reads it, so a player the hotel can see is connected is shown connected even when the
/// player grain's own flag has not caught up.
/// </summary>
public sealed class ExtendedProfileOnlineTests
{
    private const int VIEWER = 1;
    private const int PLAYER = 2;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<bool> IsShownOnlineAsync(bool grainFlag, bool hasSession)
    {
        var fakes = new Fakes();

        fakes.Handlers["GetExtendedProfileSnapshotAsync"] = _ =>
            Task.FromResult(
                (
                    (PlayerExtendedProfileSnapshot)
                        RuntimeHelpers.GetUninitializedObject(typeof(PlayerExtendedProfileSnapshot))
                ) with
                {
                    IsOnline = grainFlag,
                    Guilds = [],
                }
            );
        fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(hasSession);
        fakes.Handlers["GetMembershipsAsync"] = _ =>
            Task.FromResult(ImmutableArray<GuildInfoSnapshot>.Empty);
        fakes.Handlers["GetBadgeSummaryAsync"] = _ =>
            Task.FromResult(
                (PlayerBadgeSummarySnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(PlayerBadgeSummarySnapshot))
            );

        fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(
                (PlayerSettingsSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(PlayerSettingsSnapshot))
            );
        fakes.Handlers["GetProfileRelationAsync"] = _ =>
            Task.FromResult(
                (MessengerProfileRelationSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(MessengerProfileRelationSnapshot))
            );

        var service = new PlayerService(fakes.Create<IGrainFactory>());
        var composer = await service.GetExtendedProfileAsync(VIEWER, PLAYER, Ct);

        return ((ExtendedProfileMessageComposer)composer!).Profile.IsOnline;
    }

    [Fact]
    public async Task APlayerWithASession_IsShownOnline_EvenWhenTheirGrainFlagIsStale() =>
        (await IsShownOnlineAsync(grainFlag: false, hasSession: true)).Should().BeTrue();

    [Fact]
    public async Task APlayerWithoutASession_IsShownOffline_EvenWhenTheirGrainFlagIsStale() =>
        (await IsShownOnlineAsync(grainFlag: true, hasSession: false)).Should().BeFalse();
}
