using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A room's "Auto-kick after timeout" (navigator.roomsettings.idle_autokick) sends out a player
/// who has done nothing for that long. Habbo's rooms come with it on at 900 s (official client
/// capture of RoomSettingsData, WIN63-202610131318); Turbo stored the setting and never kicked.
/// </summary>
public sealed class IdleAutokickTests
{
    private const int AVATAR = 5;
    private const int PLAYER = 100 + AVATAR;
    private const int TIMEOUT_SECONDS = 900;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(6, 6);

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task A_player_idle_for_the_rooms_timeout_is_sent_out_only_when_the_room_says_so(
        bool enabled,
        int kicks
    )
    {
        SetAutokick(enabled);
        _room.Enter(AVATAR, 2, 2);

        await TickAsync(1_000);
        await TickAsync(1_000 + (TIMEOUT_SECONDS - 1) * 1_000L);
        Kicks().Should().Be(0, "not yet idle for the whole timeout");

        await TickAsync(1_000 + TIMEOUT_SECONDS * 1_000L);
        Kicks().Should().Be(kicks);
    }

    [Fact]
    public async Task Whatever_the_player_does_starts_the_count_again()
    {
        SetAutokick(true);
        var avatar = _room.Enter(AVATAR, 2, 2);

        await TickAsync(1_000);
        avatar.Touch(1_000 + 600_000);
        await TickAsync(1_000 + TIMEOUT_SECONDS * 1_000L);

        Kicks().Should().Be(0);
    }

    // Inference (Habbo's idle kick was not recorded): the room's own kick leaves its owner.
    [Fact]
    public async Task The_room_owner_is_not_sent_out()
    {
        SetAutokick(true);
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            OwnerId = (PlayerId)PLAYER,
        };
        _room.Enter(AVATAR, 2, 2);

        await TickAsync(1_000);
        await TickAsync(1_000 + TIMEOUT_SECONDS * 1_000L);

        Kicks().Should().Be(0);
    }

    private void SetAutokick(bool enabled)
    {
        _room.Harness.State.RoomSnapshot = _room.Harness.State.RoomSnapshot with
        {
            IdleAutokickEnabled = enabled,
            IdleAutokickTimeoutSeconds = TIMEOUT_SECONDS,
        };
    }

    private Task TickAsync(long now) =>
        _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(now, Ct);

    private int Kicks() => _room.Harness.Fakes.Log.Of("OnRemovedFromRoomAsync").Count();
}
