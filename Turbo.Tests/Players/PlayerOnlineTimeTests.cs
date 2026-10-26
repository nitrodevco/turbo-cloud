using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Players.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A player's time online in all (<c>players.online_ms</c>, read by <c>IHotelPlayers.OnlineTimeAsync</c>):
/// the presence adds each stretch of a session to it as it saves the online interval the
/// achievements count, the last one when the session ends, and tells the session's start while
/// it runs.
/// </summary>
public sealed class PlayerOnlineTimeTests : IDisposable
{
    private readonly SqliteDb _db = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task A_sessions_time_online_is_added_to_the_players_total_when_it_ends()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = SessionHarness.PlayerId,
                Name = "alice",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Online,
                OnlineMilliseconds = 60_000,
            }
        );
        var hotel = new SessionHarness();
        GrainHarness.Replace<IDbContextFactory<TurboDbContext>>(hotel.Presence, _db);
        GrainHarness.Replace(hotel.Presence, hotel.Fakes.Create<IAchievementFactRecorder>());
        var session = hotel.NewSession(1);

        await hotel.Gateway.AddSessionAsync(session.SessionKey, session);
        await hotel.Gateway.AddSessionToPlayerAsync(session.SessionKey, SessionHarness.PlayerId);

        var started = (await hotel.PresenceGrain.GetOnlineClockAsync(Ct)).SessionStartedUtc;
        started.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Ninety seconds into the session.
        var state = GrainHarness.Get<PlayerPresenceLiveState>(hotel.Presence);
        state.AchievementOnlineSinceUtc = DateTime.UtcNow.AddSeconds(-90);
        state.SessionStartedUtc = state.AchievementOnlineSinceUtc;

        await hotel.Gateway.RemoveSessionAsync(session.SessionKey, CancellationToken.None);
        await hotel.WhenPresenceIdleAsync();

        await using var dbCtx = await _db.CreateDbContextAsync(Ct);
        (await dbCtx.Players.SingleAsync(Ct))
            .OnlineMilliseconds.Should()
            .BeInRange(150_000, 155_000);
        (await hotel.PresenceGrain.GetOnlineClockAsync(Ct))
            .Should()
            .Be(new Turbo.Primitives.Players.Snapshots.PlayerOnlineClockSnapshot());
    }
}
