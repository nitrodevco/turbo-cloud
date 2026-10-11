using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Networking.Session;
using Turbo.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// What a login leaves behind: where it came from (the address, the machine) and when the
/// session ended, which is when the player was last seen. "Last seen" was the last time anything
/// wrote the player's row, so a respect given or a motto changed made an offline player look as
/// if they had just been on.
/// </summary>
public sealed class PlayerLoginTests : IDisposable
{
    private const int PLAYER = 1;

    private static readonly IReadOnlyCollection<IPAddress> LOOPBACK = ClientAddresses.Parse([
        "127.0.0.1",
        "::1",
    ]);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerLoginTests() =>
        _db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "player",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
            }
        );

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_session_is_recorded_with_its_origin_and_its_end_is_when_they_were_last_seen()
    {
        var player = await PlayerAsync();

        await player.SetOnlineStatusAsync(true, Ct);
        await player.RecordLoginOriginAsync("203.0.113.9", "machine-1", Ct);
        await player.SetOnlineStatusAsync(false, Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var login = await db.PlayerLogins.SingleAsync(Ct);
        var row = await db.Players.SingleAsync(Ct);

        (login.PlayerEntityId, login.IpAddress, login.MachineId)
            .Should()
            .Be((PLAYER, IPAddress.Parse("203.0.113.9"), "machine-1"));
        login.LoggedOutAt.Should().NotBeNull();
        row.LastSeenAt.Should().Be(login.LoggedOutAt);
        (await player.GetSummaryAsync(Ct)).LastSeen.Should().Be(row.LastSeenAt!.Value);
    }

    [Fact]
    public async Task A_change_to_an_offline_player_does_not_make_them_seen()
    {
        var player = await PlayerAsync();
        await player.SetOnlineStatusAsync(true, Ct);
        await player.SetOnlineStatusAsync(false, Ct);
        var seen = (await player.GetSummaryAsync(Ct)).LastSeen;

        await player.SetMottoAsync("a new motto", Ct);

        (await (await PlayerAsync()).GetSummaryAsync(Ct)).LastSeen.Should().Be(seen);
    }

    [Theory]
    // Straight to the server: its own address, whatever it claims.
    [InlineData("198.51.100.7", "203.0.113.9", "198.51.100.7")]
    // Through nginx on the same server: the client nginx names.
    [InlineData("127.0.0.1", "203.0.113.9", "203.0.113.9")]
    // A client that sends its own header through nginx: nginx appends the real address last.
    [InlineData("127.0.0.1", "10.9.9.9, 203.0.113.9", "203.0.113.9")]
    [InlineData("::ffff:127.0.0.1", "203.0.113.9", "203.0.113.9")]
    [InlineData("127.0.0.1", null, "127.0.0.1")]
    public void The_address_is_the_clients_and_a_header_is_believed_only_from_a_trusted_proxy(
        string connected,
        string? forwardedFor,
        string expected
    )
    {
        ClientAddresses
            .Resolve(new IPEndPoint(IPAddress.Parse(connected), 30000), forwardedFor, LOOPBACK)
            .Should()
            .Be(IPAddress.Parse(expected));
    }

    [Fact]
    public async Task The_machine_id_the_client_sends_is_kept_on_its_connection()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("UniqueIDMessageEvent"),
            PacketHarness.Payload(w =>
                w.String(" machine-1 ").String("fingerprint").String("flash")
            )
        );

        harness
            .Fakes.Log.Of("SetMachineId")
            .Should()
            .ContainSingle()
            .Which.Args[0]
            .Should()
            .Be("machine-1");
    }

    private async Task<IPlayerGrain> PlayerAsync()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerGrain",
            _fakes,
            _db,
            PLAYER
        );
        await ((Grain)grain).OnActivateAsync(Ct);

        return (IPlayerGrain)grain;
    }
}
