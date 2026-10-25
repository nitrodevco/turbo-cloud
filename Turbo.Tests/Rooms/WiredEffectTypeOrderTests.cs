using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Effects of different types in one execution run in a fixed order, whatever their stacking
/// (official client capture 2026-10-25, WIN63-202610201032, explore/wlab9-full-20261025-025051):
/// Freeze before Unfreeze (LAB-W3 wlab9 action.31.X1, 4/4: net nothing, the user walks) and
/// Change Variable before Show Message (LAB-W5 wlab9 action.40.X3b, 8/8: the message shows the
/// changed value). Ours ran them in a random order, and held the change until the end.
/// </summary>
public sealed class WiredEffectTypeOrderTests
{
    private const int CLICK_ME = 20;
    private const int USER = 5;
    private const int VARIABLE = 30;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private int _nextBox = 1;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredEffectTypeOrderTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.Enter(USER, 1, 1);
    }

    // action.31.X1 / action.32.X1: Freeze and Unfreeze on the triggerer in one execution, Freeze
    // below and on top: never left frozen, and no AvatarEffect packet (4/4).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Freeze_runs_before_unfreeze_whatever_the_stacking(bool freezeFirst)
    {
        await AddClickTriggerAsync();

        if (freezeFirst)
        {
            await AddFreezeAsync();
            await AddUnfreezeAsync();
        }
        else
        {
            await AddUnfreezeAsync();
            await AddFreezeAsync();
        }

        await RebuildAsync();

        for (var i = 0; i < 8; i++)
        {
            await ClickAsync();

            _room.Avatars[USER].IsFrozen.Should().BeFalse($"firing {i + 1}");
        }

        // No AvatarEffect at all: the room never sees the freeze effect go on and off.
        _room.Harness.Fakes.SentToRoom<AvatarEffectMessageComposer>().Should().BeEmpty();
    }

    // action.40.X3b: Change Variable +1 and Show Message "V=$(v)", Show below the change and the
    // change on top: the message shows the value after the change, 0 -> "V=1".."V=4".
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Show_message_shows_the_value_change_variable_made_in_the_same_execution(
        bool changeFirst
    )
    {
        var id = await AddRoomVariableAsync();
        await AddClickTriggerAsync();

        if (changeFirst)
        {
            await AddChangeAsync(id);
            await AddShowAsync();
        }
        else
        {
            await AddShowAsync();
            await AddChangeAsync(id);
        }

        var placeholder = _room.AddBox<WiredAddonVariablePlaceholder>(
            _nextBox,
            0,
            0,
            "wf_xtra_text_output_variable"
        );
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                _nextBox++,
                intParams: [0, (int)WiredVariableTargetType.Global, 0],
                stringParam: "v",
                variableIds: [id]
            )
        )
            .Should()
            .BeTrue();
        placeholder.Should().NotBeNull();
        await RebuildAsync();

        for (var i = 0; i < 4; i++)
            await ClickAsync();

        _room
            .Harness.Fakes.Sent<ChatMessageComposer>()
            .Select(x => x.Text)
            .Should()
            .Equal("V=1", "V=2", "V=3", "V=4");
    }

    private async Task AddClickTriggerAsync()
    {
        _room.AddBox<WiredTriggerClickFurni>(_nextBox, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                _nextBox++,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
    }

    private async Task AddFreezeAsync()
    {
        _room.AddBox<WiredActionFreezeUser>(_nextBox, 0, 0, "wf_act_freeze");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                _nextBox++,
                intParams: [0, 0],
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task AddUnfreezeAsync()
    {
        _room.AddBox<WiredActionUnfreezeUser>(_nextBox, 0, 0, "wf_act_unfreeze");
        (await _room.SaveAsync<UpdateActionMessage>(_nextBox++, definitionSpecifics: [0]))
            .Should()
            .BeTrue();
    }

    private async Task<string> AddRoomVariableAsync()
    {
        var variable = _room.AddBox<WiredVariableRoom>(VARIABLE, 4, 4, "wf_var_room");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                VARIABLE,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "w9o"
            )
        )
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(Ct);
        await RebuildAsync(4, 4);

        return variable.GetVarSnapshot().VariableId.ToString();
    }

    private async Task AddChangeAsync(string variableId)
    {
        _room.AddBox<WiredActionChangeVariable>(_nextBox, 0, 0, "wf_act_change_var_val");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                _nextBox++,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Add,
                    0,
                    0,
                    1,
                    (int)WiredVariableTargetType.Global,
                    0,
                ],
                definitionSpecifics: [0],
                variableIds: [variableId, "0"]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task AddShowAsync()
    {
        _room.AddBox<WiredActionShowMessage>(_nextBox, 0, 0, "wf_act_show_message");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                _nextBox++,
                intParams: [1, 34, -1],
                stringParam: "V=$(v)",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
    }

    private async Task RebuildAsync(int x = 0, int y = 0)
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

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(
                ActionContext.CreateForPlayer((PlayerId)(100 + USER), (RoomId)1),
                0,
                Ct
            );
        await TickAsync(4);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 500;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
