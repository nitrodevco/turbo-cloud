using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Saves Habbo refuses with a <c>wiredfurni.error.*</c> text of their own, sent with the
/// parameters the client fills into it (<c>IncomingMessages.onValidationError</c>, AS3
/// WIN63-202610131318): a text over the box's limit (<c>too_much_text</c>, %limit%), a counter
/// box picking something that is not a counter (<c>require_counter_furni</c>) and a variable fx
/// range whose maximum is not above its minimum (<c>variable_fx.value_range</c>). Ours cut the
/// text, kept the pick and repaired the range, and told the editor nothing.
/// </summary>
public sealed class WiredSaveErrorKeysTests
{
    private const int BOX = 2;
    private const int COUNTER = 3;
    private const int LAMP = 4;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task A_text_over_the_boxs_limit_is_refused_with_the_limit()
    {
        _room.AddBox<WiredActionShowMessage>(BOX, 0, 0, "wf_act_show_message");
        var limit = new Turbo.Rooms.Configuration.WiredConfig().ShowMessageMaxLength;

        var refused = await SaveMessageAsync(new string('a', limit + 1));

        refused.ErrorKey.Should().Be(WiredSaveErrors.TOO_MUCH_TEXT);
        refused.Parameters.Should().Equal(("limit", limit.ToString()));
        (await SaveMessageAsync(new string('a', limit))).IsSaved.Should().BeTrue();
    }

    private Task<WiredSaveResult> SaveMessageAsync(string text) =>
        _room.SaveWithResultAsync<UpdateActionMessage>(
            BOX,
            intParams: [1, 34, -1],
            stringParam: text,
            definitionSpecifics: [0]
        );

    [Fact]
    public async Task A_counter_box_takes_counters_only()
    {
        _room.AddBox<WiredActionControlClock>(BOX, 0, 0, "wf_act_control_clock");
        _room.AddFloorItem(
            COUNTER,
            1,
            0,
            "wf_upcounter1",
            createLogic: (factory, ctx) => new FurnitureCounterClock1Logic(factory, ctx)
        );
        _room.AddFloorItem(LAMP, 2, 0);

        (
            await _room.SaveWithResultAsync<UpdateActionMessage>(
                BOX,
                intParams: [1],
                stuffIds: [LAMP]
            )
        )
            .ErrorKey.Should()
            .Be(WiredSaveErrors.REQUIRE_COUNTER_FURNI);
        (
            await _room.SaveWithResultAsync<UpdateActionMessage>(
                BOX,
                intParams: [1],
                stuffIds: [COUNTER]
            )
        )
            .IsSaved.Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(10, 10, false)]
    [InlineData(10, 5, false)]
    [InlineData(10, 11, true)]
    public async Task A_variable_fx_range_needs_its_maximum_above_its_minimum(
        int min,
        int max,
        bool saved
    )
    {
        var box = _room.AddBox<WiredAddonVariableFxProgressBar>(BOX, 0, 0, "wf_xtra_varfx_prog");
        var intParams = box.GetDefaultIntParams().ToArray();
        intParams[11] = min;
        intParams[13] = max;

        var result = await _room.SaveWithResultAsync<UpdateAddonMessage>(BOX, intParams: intParams);

        result.IsSaved.Should().Be(saved);

        if (!saved)
            result.ErrorKey.Should().Be(WiredSaveErrors.VARIABLE_FX_VALUE_RANGE);
    }
}
