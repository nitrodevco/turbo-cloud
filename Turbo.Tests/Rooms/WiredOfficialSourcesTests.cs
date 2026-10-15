using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The sources the editor is offered on boxes whose input slots read their sources the usual way,
/// as the official client capture of each box's InputSourcesConf lists them (allowed per slot,
/// sorted, and the slot's default).
/// </summary>
public sealed class WiredOfficialSourcesTests
{
    private static readonly int[] Furni = [0, 100, 200, 201];
    private static readonly int[] Both = [0, 100, 101, 200, 201];
    private static readonly int[] Users = [0, 200, 201];
    private static readonly int[] Bots = [0, 100, 200, 201];

    private readonly WiredRoom _room = new(8, 8);

    public static TheoryData<Type, string, int[][], int[], int[][], int[]> OfficialContracts =>
        new()
        {
            // logic, name, allowed furni, default furni, allowed users, default users
            {
                typeof(WiredConditionCounterTimeMatch),
                "wf_cnd_counter_time_matches",
                [Furni],
                [100],
                [],
                []
            },
            { typeof(WiredActionAdjustClock), "wf_act_adjust_clock", [Furni], [100], [], [] },
            { typeof(WiredActionControlClock), "wf_act_control_clock", [Furni], [100], [], [] },
            {
                typeof(WiredActionCancelTransaction),
                "wf_act_cancel_transaction",
                [Furni],
                [100],
                [Users],
                [0]
            },
            {
                typeof(WiredActionTeleportToRoom),
                "wf_act_teleport_to_room",
                [Furni],
                [100],
                [Users],
                [0]
            },
            { typeof(WiredActionCallStacks), "wf_act_call_stacks", [Furni], [100], [], [] },
            {
                typeof(WiredConditionExecutorOnItem),
                "wf_cnd_trggrer_on_frn",
                [Furni],
                [100],
                [Users],
                [0]
            },
            { typeof(WiredActionShowMessage), "wf_act_show_message", [], [], [Users], [0] },
            { typeof(WiredActionMatchToSnapshot), "wf_act_match_to_sshot", [Furni], [100], [], [] },
            { typeof(WiredConditionHasVariable), "wf_cnd_has_var", [Furni], [100], [Users], [0] },
            {
                typeof(WiredAddonVariablePlaceholder),
                "wf_xtra_text_output_variable",
                [Furni],
                [100],
                [Users],
                [0]
            },
            {
                typeof(WiredSelectorItemsWithVariable),
                "wf_slc_furni_with_var",
                [
                    [0, 100, 201],
                ],
                [100],
                [
                    [0, 201],
                ],
                [0]
            },
            {
                typeof(WiredSelectorEntitiesWithVariable),
                "wf_slc_users_with_var",
                [
                    [0, 100, 201],
                ],
                [100],
                [
                    [0, 201],
                ],
                [0]
            },
            // Recorded for codes 39 and 43 on 2026-10-12, like 41 and 42.
            {
                typeof(WiredActionGiveVariable),
                "wf_act_give_var",
                [Both, Both],
                [0, 200],
                [Users, Users],
                [0, 200]
            },
            {
                typeof(WiredConditionVariableAgeMatch),
                "wf_cnd_var_age_match",
                [Both, Both],
                [0, 200],
                [Users, Users],
                [0, 200]
            },
            {
                typeof(WiredActionChangeVariable),
                "wf_act_change_var_val",
                [Both, Both],
                [0, 200],
                [Users, Users],
                [0, 200]
            },
            {
                typeof(WiredConditionVariableValueMatch),
                "wf_cnd_var_val_match",
                [Both, Both],
                [0, 200],
                [Users, Users],
                [0, 200]
            },
            {
                typeof(WiredActionPlaceFurni),
                "wf_act_place_furni",
                [
                    [0, 100, 101, 110, 200, 201],
                    Both,
                    Both,
                ],
                [110, 0, 200],
                [Users, Users],
                [0, 200]
            },
            // Recorded 2026-10-14 on WIN63-202610131318.
            {
                typeof(WiredActionBotTalk),
                "wf_act_bot_talk",
                [],
                [],
                [
                    [0, 10, 100, 200, 201],
                ],
                [100]
            },
            {
                typeof(WiredSelectorEntitiesOnItem),
                "wf_slc_users_onfurni",
                [
                    [0, 100, 201],
                ],
                [100],
                [],
                []
            },
            {
                typeof(WiredSelectorRemoteSelection),
                "wf_slc_remote",
                [
                    [0, 100, 201],
                ],
                [100],
                [],
                []
            },
            {
                typeof(WiredConditionSelectQuantity),
                "wf_cnd_slc_quantity",
                [Users],
                [200],
                [Users],
                [200]
            },
            {
                typeof(WiredConditionExecutorMatches),
                "wf_cnd_triggerer_match",
                [],
                [],
                [Users, [0, 101, 200, 201]],
                [0, 101]
            },
            { typeof(WiredConditionTeamHasRank), "wf_cnd_team_has_rank", [], [], [Users], [0] },
            { typeof(WiredConditionTeamHasScore), "wf_cnd_team_has_score", [], [], [Users], [0] },
            // The other bot boxes: user slot 0 is the bots (sources.users.title.bots), the boxes
            // that act on users too have the users in slot 1.
            {
                typeof(WiredActionBotTeleport),
                "wf_act_bot_teleport",
                [Furni],
                [100],
                [Bots],
                [100]
            },
            { typeof(WiredActionBotMove), "wf_act_bot_move", [Furni], [100], [Bots], [100] },
            { typeof(WiredActionBotChangeFigure), "wf_act_bot_clothes", [], [], [Bots], [100] },
            {
                typeof(WiredActionBotGiveHandItem),
                "wf_act_bot_give_handitem",
                [],
                [],
                [Bots, Users],
                [100, 0]
            },
            {
                typeof(WiredActionBotFollowAvatar),
                "wf_act_bot_follow_avatar",
                [],
                [],
                [Bots, Users],
                [100, 0]
            },
            {
                typeof(WiredActionBotTalkToAvatar),
                "wf_act_bot_talk_to_avatar",
                [],
                [],
                [Bots, Users],
                [100, 0]
            },
            {
                typeof(WiredTriggerBotReachesHabbo),
                "wf_trg_bot_reached_avtr",
                [],
                [],
                [
                    [100, 200],
                ],
                [100]
            },
            {
                typeof(WiredAddonFurniSelectorFilter),
                "wf_xtra_filter_furni",
                [
                    [0, 100, 201],
                ],
                [100],
                [
                    [0, 201],
                ],
                [0]
            },
            {
                typeof(WiredAddonFilterUsersByVariable),
                "wf_xtra_filter_users_by_var",
                [
                    [0, 100, 201],
                ],
                [100],
                [
                    [0, 201],
                ],
                [0]
            },
            {
                typeof(WiredConditionItemPerType),
                "wf_cnd_stuff_is",
                [Both, Both],
                [0, 100],
                [],
                []
            },
            {
                typeof(WiredActionRemoveFurni),
                "wf_act_remove_furni",
                [
                    [0, 200, 201],
                ],
                [200],
                [],
                []
            },
        };

    [Theory]
    [MemberData(nameof(OfficialContracts))]
    public void The_editor_is_offered_the_official_sources_and_defaults(
        Type logic,
        string name,
        int[][] furni,
        int[] defaultFurni,
        int[][] users,
        int[] defaultUsers
    )
    {
        var box = (FurnitureWiredLogic)
            typeof(WiredRoom)
                .GetMethod(nameof(WiredRoom.AddBox))!
                .MakeGenericMethod(logic)
                .Invoke(_room, [1, 0, 0, name])!;

        box.GetAllowedFurniSources()
            .Select(slot =>
                slot.Select(x => (int)WiredFurniSourceTypeExtensions.GetProtocolId(x))
                    .Order()
                    .ToArray()
            )
            .Should()
            .BeEquivalentTo(furni, o => o.WithStrictOrdering());
        box.GetDefaultFurniSources()
            .Select(slot => (int)WiredFurniSourceTypeExtensions.GetProtocolId(slot.Single()))
            .Should()
            .Equal(defaultFurni);
        box.GetAllowedPlayerSources()
            .Select(slot =>
                slot.Select(x => (int)WiredPlayerSourceTypeExtensions.GetProtocolId(x))
                    .Order()
                    .ToArray()
            )
            .Should()
            .BeEquivalentTo(users, o => o.WithStrictOrdering());
        box.GetDefaultPlayerSources()
            .Select(slot => (int)WiredPlayerSourceTypeExtensions.GetProtocolId(slot.Single()))
            .Should()
            .Equal(defaultUsers);
    }

    /// <summary>
    /// "Use the triggering user" on Bot Talk with no bot name and no message saves (official client
    /// capture: UpdateAction ints [0,-1,0], string "\t", user sources [0] -> WiredSaveSuccess).
    /// </summary>
    [Fact]
    public async Task Bot_talk_saves_with_the_triggering_user()
    {
        var talk = _room.AddBox<WiredActionBotTalk>(1, 0, 0, "wf_act_bot_talk");

        (
            await _room.SaveAsync<Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.UpdateActionMessage>(
                1,
                intParams: [0, -1, 0],
                stringParam: "\t",
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        talk.GetPlayerSources().Single().Should().Equal(WiredPlayerSourceType.TriggeredUser);
    }

    /// <summary>
    /// A follow, hand item or talk to box saved while the users came first and the bot second
    /// loads with the bot in slot 0 and the users in slot 1, so it keeps doing what it did.
    /// </summary>
    [Fact]
    public async Task A_bot_box_saved_with_the_users_first_keeps_its_bot_and_users()
    {
        var follow = _room.AddBox<WiredActionBotFollowAvatar>(1, 0, 0, "wf_act_bot_follow_avatar");

        _room
            .FloorItem(1)
            .ExtraData.UpdateSection(
                Turbo.Primitives.Furniture.Enums.ExtraDataSectionType.WIRED,
                System.Text.Json.JsonSerializer.SerializeToNode(
                    new Turbo.Rooms.Wired.WiredData
                    {
                        StringParam = "Frank",
                        PlayerSources =
                        [
                            [WiredPlayerSourceType.SelectorUsers],
                            [WiredPlayerSourceType.BotByName],
                        ],
                    }
                )!
            );
        await follow.LoadWiredAsync(TestContext.Current.CancellationToken);

        follow
            .GetPlayerSources()
            .Should()
            .BeEquivalentTo(
                new[]
                {
                    new[] { WiredPlayerSourceType.BotByName },
                    [WiredPlayerSourceType.SelectorUsers],
                },
                o => o.WithStrictOrdering()
            );
    }
}
