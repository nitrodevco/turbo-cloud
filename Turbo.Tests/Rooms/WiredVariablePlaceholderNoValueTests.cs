using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A Variable Placeholder whose variable exists but holds no value on the holder it reads prints
/// "null" (official client capture 2026-10-25, WIN63-202610201032): a context variable nobody gave
/// in the execution, "C=$(c)" -> "C=null" (wired matrix variable.3.X1 4/4, addon.16.R3 2/2), and a
/// delayed message whose selected user left, "V=$(v)" -> "V=null" in text mode with a connector,
/// which does not map it (addon.1000.B2 2/2). Ours printed nothing ("C=").
/// </summary>
public sealed class WiredVariablePlaceholderNoValueTests
{
    private const int PLAYER_INDEX = 5;
    private const int LEFT_INDEX = 9;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_context_variable_nobody_gave_prints_null()
    {
        var context = _room.AddBox<WiredVariableContext>(10, 6, 6, "wf_var_context");

        (await _room.SaveAsync<UpdateVariableMessage>(10, intParams: [1], stringParam: "c"))
            .Should()
            .BeTrue();
        await LoadAsync(context);

        var ctx = await PlaceholderAsync(
            context.GetVarSnapshot().VariableId.ToString(),
            WiredVariableTargetType.Context,
            "c"
        );

        (await ctx.FormatTextAsync("C=$(c)", Ct)).Should().Be("C=null");
    }

    [Fact]
    public async Task A_selected_user_without_the_value_prints_null_past_the_connector()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);

        var variable = _room.AddBox<WiredVariableUser>(10, 6, 6, "wf_var_user");
        _room.AddBox<WiredAddonVariableTextConnector>(11, 6, 6, "wf_xtra_var_text_connector");

        (await _room.SaveAsync<UpdateAddonMessage>(11, stringParam: "7=seven")).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "v"
            )
        )
            .Should()
            .BeTrue();
        await LoadAsync(variable);

        var ctx = await PlaceholderAsync(
            variable.GetVarSnapshot().VariableId.ToString(),
            WiredVariableTargetType.User,
            "v",
            textMode: true
        );

        // The selection keeps the user who left; no value is held for them any more.
        ctx.Selected.SelectedAvatarIds.Add(LEFT_INDEX);

        (await ctx.FormatTextAsync("V=$(v)", Ct)).Should().Be("V=null");
    }

    // addon.15.R1b (official client capture 2026-10-25T09-13-22, 2/2): single mode over an empty
    // user selection leaves the token as written, as Username does (addon.14.X6). Ours printed "V=[]".
    [Fact]
    public async Task An_empty_selection_leaves_a_single_value_as_written()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);

        var variable = _room.AddBox<WiredVariableUser>(10, 6, 6, "wf_var_user");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.UserActive, 1],
                stringParam: "v"
            )
        )
            .Should()
            .BeTrue();
        await LoadAsync(variable);

        var ctx = await PlaceholderAsync(
            variable.GetVarSnapshot().VariableId.ToString(),
            WiredVariableTargetType.User,
            "v"
        );

        (await ctx.FormatTextAsync("V=[$(v)]", Ct)).Should().Be("V=[$(v)]");
    }

    private async Task<WiredExecutionContext> PlaceholderAsync(
        string variableId,
        WiredVariableTargetType target,
        string name,
        bool textMode = false
    )
    {
        var placeholder = _room.AddBox<WiredAddonVariablePlaceholder>(
            1,
            0,
            0,
            "wf_xtra_text_output_variable"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                1,
                intParams: [0, (int)target, textMode ? 1 : 0],
                stringParam: name,
                variableIds: [variableId]
            )
        )
            .Should()
            .BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Policy.TextPlaceholders.Add(placeholder);

        return ctx;
    }

    private async Task LoadAsync(FurnitureWiredVariableLogic variable)
    {
        await variable.LoadWiredAsync(Ct);
        await _room
            .Harness.Module<RoomWiredSystem>()
            .OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [_room.Map.ToIdx(6, 6)],
                },
                Ct
            );
        await _room.Harness.Module<RoomWiredSystem>().ProcessWiredAsync(10_000, dormant: false, Ct);
    }
}
