using System.Net;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Players;
using Turbo.Main;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Networking;

/// <summary>
/// A player online when the server stops is logged out: their login gets its logout time and
/// they get a last-seen time. Closing the connections started that work and nothing waited for
/// it, so the silo stopped first: measured with 200 load bots online, 110 to 114 logins were
/// left open. The server now ends every session before it closes the connections.
/// </summary>
public sealed class ShutdownLogoutTests : IDisposable
{
    private const int PLAYER = SessionHarness.PlayerId;

    private readonly SqliteDb _db = new();
    private readonly SessionHarness _sessions = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ShutdownLogoutTests()
    {
        _db.InsertOwnersAndRooms([PLAYER], [1]);
        _db.Insert(
            new PlayerLoginEntity
            {
                Id = 1,
                PlayerEntityId = PLAYER,
                IpAddress = IPAddress.Loopback,
            }
        );

        // The player's own grain, on the database, online as a login leaves it.
        var player = GrainHarness.Create(
            typeof(Turbo.Players.PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerGrain",
            _sessions.Fakes,
            _db,
            PLAYER
        );
        RoomHarness.SetMember(RoomHarness.GetField(player, "_state")!, "IsOnline", true);
        _sessions.Fakes.Instances[(typeof(IPlayerGrain), (object)(long)PLAYER)] = player;
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_player_online_when_the_server_stops_is_logged_out()
    {
        var key = SessionHarness.MakeKey(1);
        await _sessions.Gateway.AddSessionAsync(key, _sessions.NewSession(1));
        await _sessions.Gateway.AddSessionToPlayerAsync(key, PLAYER);

        // The connections close, but their close handlers never get to run before the silo stops.
        await Emulator().StopAsync(Ct);
        await SessionHarness.Settle();

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.PlayerLogins.SingleAsync(Ct)).LoggedOutAt.Should().NotBeNull();
        (await db.Players.SingleAsync(x => x.Id == PLAYER, Ct)).LastSeenAt.Should().NotBeNull();
        _sessions.Gateway.GetOnlinePlayerIds().Should().BeEmpty();
    }

    /// <summary>The emulator as the host builds it, with the real session gateway.</summary>
    private TurboEmulator Emulator()
    {
        var ctor = typeof(TurboEmulator).GetConstructors().Single();

        return (TurboEmulator)
            ctor.Invoke(
                ctor.GetParameters()
                    .Select(p =>
                        p.ParameterType == typeof(ISessionGateway) ? _sessions.Gateway
                        : p.ParameterType == typeof(ILogger<TurboEmulator>)
                            ? NullLogger<TurboEmulator>.Instance
                        : _sessions.Fakes.Create(p.ParameterType)
                    )
                    .ToArray()
            );
    }
}
