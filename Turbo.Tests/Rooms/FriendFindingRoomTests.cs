using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Find Friends (<c>FindNewFriends</c>): Habbo answers <c>FindFriendsProcessResult</c> true and
/// forwards the player into another user's populated room (official client capture 2026-10-14,
/// WIN63-202610131318, sent from the player's own room). Never their own room or the one they are in.
/// </summary>
public sealed class FriendFindingRoomTests
{
    private const int PLAYER = 7;
    private const int OTHER = 8;

    private readonly IRoomDirectoryGrain _directory = (IRoomDirectoryGrain)
        GrainHarness.Create<RoomDirectoryGrain>(new Fakes());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_friend_finding_room_is_someone_elses_room_the_player_is_not_in()
    {
        await RoomAsync(1, owner: PLAYER, players: [PLAYER]); // their own room, where they stand
        await RoomAsync(2, owner: PLAYER, players: [OTHER]); // another of their rooms
        await RoomAsync(3, owner: OTHER, players: [PLAYER, OTHER]); // someone's room they are in
        await RoomAsync(4, owner: OTHER, players: []); // empty
        await RoomAsync(5, owner: OTHER, players: [OTHER]);

        for (var i = 0; i < 20; i++)
            (await _directory.GetFriendFindingRoomAsync(PLAYER, Ct)).Should().Be((RoomId)5);
    }

    [Fact]
    public async Task With_only_the_players_own_populated_room_there_is_none()
    {
        await RoomAsync(1, owner: PLAYER, players: [PLAYER]);

        (await _directory.GetFriendFindingRoomAsync(PLAYER, Ct)).Should().BeNull();
        (await _directory.GetRandomPopulatedRoomAsync(Ct))
            .Should()
            .Be((RoomId)1, "a random room may still be any populated one");
    }

    private async Task RoomAsync(int roomId, int owner, int[] players)
    {
        var room = (RoomInfoSnapshot)
            RuntimeHelpers.GetUninitializedObject(typeof(RoomInfoSnapshot)) with
        {
            RoomId = (RoomId)roomId,
            OwnerId = (PlayerId)owner,
        };

        await _directory.UpsertActiveRoomAsync(room, Ct);

        foreach (var player in players)
            await _directory.AddPlayerToRoomAsync(player, roomId, Ct);
    }
}
