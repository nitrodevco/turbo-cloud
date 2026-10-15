using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The boxes whose editors are in Habbo's client but which this server did not build (Flash
/// <c>ActionTypeCodes.GIVE_EFFECT</c> 52 / <c>OVERRIDE_HEIGHT</c> 53,
/// <c>AddonCodes.GLOBAL_PLACEHOLDER</c> 2000, <c>VariableCodes</c> 8 daily task): each saves
/// what its editor writes and does what its fields say.
/// </summary>
public sealed class WiredUnreleasedBoxesTests
{
    private const int PLAYER_INDEX = 5;
    private const int CLICK_ME = 23;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    /// <summary>
    /// <c>actiontypes._-Xm</c>: [effect id 0..10000, priority 0..2, type 0/1]. A user clicks a
    /// furni and wears the effect.
    /// </summary>
    [Fact]
    public async Task Give_effect_puts_its_effect_on_the_selected_users()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionGiveEffect>(2, 0, 0, "wf_act_give_effect");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [44, 2, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await RebuildAsync(0, 0);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER_INDEX), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(4);

        _room.Avatars[PLAYER_INDEX].EffectId.Should().Be(44);
    }

    [Theory]
    [InlineData(new[] { 13, 2, 0 }, true)]
    [InlineData(new[] { 10000, 0, 1 }, true)]
    [InlineData(new[] { 13, 3, 0 }, false)]
    [InlineData(new[] { 10001, 2, 0 }, false)]
    public async Task Give_effect_saves_within_its_editors_ranges(int[] intParams, bool saved)
    {
        _room.AddBox<WiredActionGiveEffect>(2, 0, 0, "wf_act_give_effect");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: intParams,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .Be(saved);
    }

    /// <summary><c>actiontypes._-O18</c>: [height 0..8000, type 0/1].</summary>
    [Theory]
    [InlineData(new[] { 0, 0 }, true)]
    [InlineData(new[] { 8000, 1 }, true)]
    [InlineData(new[] { 8001, 0 }, false)]
    public async Task Override_height_saves_within_its_editors_ranges(int[] intParams, bool saved)
    {
        _room.AddBox<WiredActionOverrideHeight>(2, 0, 0, "wf_act_override_height");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: intParams,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .Be(saved);
    }

    /// <summary>
    /// <c>GlobalPlaceholderAddon</c>: [type, 0, room id] and "name\tvalue". The add-on stands on a
    /// tile of its own and the text is another stack's, which has no placeholder of its own.
    /// </summary>
    [Theory]
    [InlineData(0, "Hi $(greeting)!", "Hi Hello there!")]
    [InlineData(1, "Hi $(greeting)!", "Hi !")]
    [InlineData(0, "Hi $(other)!", "Hi $(other)!")]
    public async Task A_global_placeholder_fills_every_stacks_text(
        int type,
        string text,
        string shown
    )
    {
        _room.AddBox<WiredAddonGlobalPlaceholder>(3, 4, 4, "wf_xtra_global_placeholder");

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                3,
                intParams: [type, 0, 0],
                stringParam: "greeting\tHello there"
            )
        )
            .Should()
            .BeTrue();

        await RebuildAsync(4, 4);

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        (await ctx.FormatTextAsync(text, Ct)).Should().Be(shown);
    }

    /// <summary><c>conditions.LevelMatches</c>: [level 1..30, comparison 0..2].</summary>
    [Theory]
    [InlineData(new[] { 1, 0 }, true)]
    [InlineData(new[] { 30, 2 }, true)]
    [InlineData(new[] { 0, 1 }, false)]
    [InlineData(new[] { 31, 1 }, false)]
    [InlineData(new[] { 5, 3 }, false)]
    public async Task User_level_saves_within_its_editors_ranges(int[] intParams, bool saved)
    {
        _room.AddBox<WiredConditionUserLevel>(2, 0, 0, "wf_cnd_user_level");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                2,
                intParams: intParams,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .Be(saved);
    }

    /// <summary>
    /// The level compared is the room's <c>@level</c>; without it there is nothing to compare,
    /// whatever the comparison.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task User_level_is_not_met_without_an_avatar_level(int comparison)
    {
        _room.Enter(PLAYER_INDEX, 1, 1);

        var condition = _room.AddBox<WiredConditionUserLevel>(2, 0, 0, "wf_cnd_user_level");

        (
            await _room.SaveAsync<UpdateConditionMessage>(
                2,
                intParams: [1, comparison],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        condition.Evaluate(ctx).Should().BeFalse();
    }

    /// <summary>
    /// <c>DailyTaskVariable</c> saves "name\tcode": the variable is called by the name alone.
    /// </summary>
    [Fact]
    public async Task The_daily_task_variable_is_named_without_its_code()
    {
        var variable = _room.AddBox<WiredVariableDailyTask>(4, 2, 2, "wf_var_dailytask");

        (await _room.SaveAsync<UpdateVariableMessage>(4, stringParam: "my_task\t1234"))
            .Should()
            .BeTrue();

        // As the room does when it is told the box changed.
        await variable.LoadWiredAsync(Ct);

        variable.GetVarSnapshot().VariableName.Should().Be("my_task");
        variable.GetVarSnapshot().TargetType.Should().Be(WiredVariableTargetType.User);
        variable.TaskCode.Should().Be("1234");
    }

    private async Task RebuildAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
