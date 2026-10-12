using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Navigator;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// A room Habbo creates (a new account's starter room, hhus, 2026-10-12) has: leave on the door
/// tile, idle sleep after 300 s, idle kick after 900 s, normal chat flood. Turbo created rooms
/// with all of it off and the minimal chat flood.
/// </summary>
public sealed class NewRoomDefaultsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_new_room_starts_with_habbos_settings()
    {
        var db = new InMemoryDb();
        using var provider = new NavigatorProvider(
            db,
            Options.Create(new NavigatorConfig()),
            NullLogger<NavigatorProvider>.Instance
        );

        var roomId = await provider.CreateRoomAsync(
            (PlayerId)1,
            "room",
            "",
            1,
            null,
            25,
            RoomTradeModeType.Disabled,
            Ct
        );

        await using var ctx = await db.CreateDbContextAsync(Ct);
        var room = await ctx.Rooms.SingleAsync(x => x.Id == roomId.Value, Ct);

        room.LeaveOnDoorTile.Should().BeTrue();
        (room.IdleSleepEnabled, room.IdleSleepTimeoutSeconds).Should().Be((true, 300));
        (room.IdleAutokickEnabled, room.IdleAutokickTimeoutSeconds).Should().Be((true, 900));
        room.ChatFloodType.Should().Be(ChatFloodSensitivityType.Normal);
    }
}
