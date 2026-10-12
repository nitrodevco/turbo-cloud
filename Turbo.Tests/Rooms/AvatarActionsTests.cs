using FluentAssertions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The avatar menu's actions as Habbo answers them (hhus, 2026-10-12): waving while dancing
/// stops the dance first (Dance 0, then the wave), and Sit sits at 0.55 ("sit 0.55").
/// </summary>
public sealed class AvatarActionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_wave_stops_a_dance()
    {
        var room = new WiredRoom(6, 6);
        var avatar = room.Enter(1, 2, 2);
        var avatars = room.Harness.Module<RoomAvatarModule>();

        await avatars.SetAvatarDanceAsync(avatar.ObjectId, AvatarDanceType.Dance, Ct);
        await avatars.SetAvatarExpressionAsync(avatar.ObjectId, AvatarExpressionType.Wave, Ct);

        avatar.DanceType.Should().Be(AvatarDanceType.None);
    }

    [Fact]
    public async Task Sitting_down_on_the_floor_sits_at_habbos_height()
    {
        var room = new WiredRoom(6, 6);
        var avatar = room.Enter(1, 2, 2);

        await room
            .Harness.Module<RoomAvatarModule>()
            .SetAvatarPostureAsync(avatar.ObjectId, AvatarPostureType.Sit, Ct);

        avatar.Statuses[AvatarStatusType.Sit].Should().Be("0.55");
    }
}
