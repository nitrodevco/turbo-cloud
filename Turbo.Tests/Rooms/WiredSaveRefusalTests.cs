using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A save Habbo refuses without a text of its own: answered with the server error the client shows
/// as "Sorry, received server error: 1", and nothing of it stored (official client capture
/// 2026-10-24: Chase saved with 21 picks of 20, Join Team with team 0 and 5). Each is a hotel
/// setting; off keeps the earlier behaviour.
/// </summary>
public sealed class WiredSaveRefusalTests
{
    private const int CHASE = 1;
    private const int JOIN_TEAM = 2;
    private const int FIRST_FURNI = 30;

    private readonly WiredRoom _room = new(8, 8);

    public WiredSaveRefusalTests()
    {
        _room.AddBox<WiredActionChaseHabbo>(CHASE, 0, 0, "wf_act_chase");
        _room.AddBox<WiredActionJoinTeam>(JOIN_TEAM, 1, 0, "wf_act_join_team");

        for (var i = 0; i < 22; i++)
            _room.AddFloorItem(FIRST_FURNI + i, i % 8, 1 + (i / 8));
    }

    private WiredActionChaseHabbo Chase => (WiredActionChaseHabbo)_room.FloorItem(CHASE).Logic;

    private WiredActionJoinTeam JoinTeam => (WiredActionJoinTeam)_room.FloorItem(JOIN_TEAM).Logic;

    private static int[] Picks(int first, int count) =>
        [.. Enumerable.Range(FIRST_FURNI + first, count)];

    private Task<WiredSaveResult> SaveChaseAsync(int[] picks) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            CHASE,
            stuffIds: picks,
            definitionSpecifics: [0]
        );

    private Task<WiredSaveResult> SaveJoinTeamAsync(int team) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            JOIN_TEAM,
            intParams: [team, 0],
            definitionSpecifics: [0]
        );

    [Fact]
    public async Task A_save_with_21_picks_of_20_is_a_server_error_and_keeps_the_earlier_picks_action_11_E2()
    {
        (await SaveChaseAsync(Picks(0, 3))).IsSaved.Should().BeTrue();

        var refused = await SaveChaseAsync(Picks(1, 21));

        refused.IsSaved.Should().BeFalse();
        refused.ServerErrorCode.Should().Be(1);
        refused.ErrorKey.Should().BeNull();
        Chase.GetStuffIds().Should().Equal(Picks(0, 3));
    }

    [Fact]
    public async Task A_save_with_20_picks_of_20_saves_them_all_action_11_E2()
    {
        (await SaveChaseAsync(Picks(2, 20))).IsSaved.Should().BeTrue();

        Chase.GetStuffIds().Should().BeEquivalentTo(Picks(2, 20));
    }

    [Fact]
    public async Task A_hotel_that_turns_the_refusal_off_keeps_the_first_20_picks_action_11_E2()
    {
        _room.Harness.Room.UseWiredConfig(new WiredConfig { RefuseOverLimitPicks = false });

        (await SaveChaseAsync(Picks(0, 21))).IsSaved.Should().BeTrue();

        Chase.GetStuffIds().Should().Equal(Picks(0, 20));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    public async Task Join_team_with_team_0_or_5_is_a_server_error_and_keeps_the_saved_team_action_9_E11(
        int team
    )
    {
        (await SaveJoinTeamAsync(2)).IsSaved.Should().BeTrue();

        var refused = await SaveJoinTeamAsync(team);

        refused.IsSaved.Should().BeFalse();
        refused.ServerErrorCode.Should().Be(1);
        JoinTeam.GetSnapshot().IntParams.Should().Equal(2, 0);
    }

    [Fact]
    public async Task A_hotel_that_turns_the_server_error_off_answers_with_its_own_text_action_9_E11()
    {
        _room.Harness.Room.UseWiredConfig(new WiredConfig { RefusalsAsServerError = false });

        var refused = await SaveJoinTeamAsync(5);

        refused.IsSaved.Should().BeFalse();
        refused.ServerErrorCode.Should().BeNull();
        refused.ErrorKey.Should().BeNull();
    }

    [Fact]
    public async Task A_refusal_with_a_hotel_text_keeps_its_text_not_the_server_error()
    {
        _room.AddBox<WiredActionBotFollowAvatar>(3, 2, 0, "wf_act_bot_follow_avatar");

        var refused = await _room.SaveWithResultAsync<UpdateActionMessage>(
            3,
            intParams: [0],
            stringParam: "Frank",
            playerSources:
            [
                [Turbo.Primitives.Rooms.Enums.Wired.WiredPlayerSourceType.BotByName],
                [Turbo.Primitives.Rooms.Enums.Wired.WiredPlayerSourceType.BotByName],
            ]
        );

        refused.ErrorKey.Should().Be("wiredfurni.error.invalid_user_source");
        refused.ServerErrorCode.Should().BeNull();
    }
}
