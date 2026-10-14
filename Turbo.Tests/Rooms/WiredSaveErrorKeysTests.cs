using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Saves as Habbo answers them, with the parameters the client fills into the text
/// (<c>IncomingMessages.onValidationError</c>, AS3 WIN63-202610131318). Official client capture
/// 2026-10-14 (NOTES-iv-save-errors): Users By Name refuses a 31-character name with
/// <c>username_length</c> {max_username_length: "30"} and 21 names with <c>usernames_count</c>
/// {max_usernames: "20"}; a 600-character Show Message saves and reopens cut to 400; a variable fx
/// range with max below min saves as max = min + 1. A counter box picking something that is not a
/// counter is refused with <c>require_counter_furni</c> (inference: which boxes the text is for).
/// </summary>
public sealed class WiredSaveErrorKeysTests
{
    private const int BOX = 2;
    private const int COUNTER = 3;
    private const int LAMP = 4;

    private readonly WiredRoom _room = new(8, 8);

    [Fact]
    public async Task A_text_over_the_boxs_limit_is_saved_cut_without_an_error()
    {
        var box = _room.AddBox<WiredActionShowMessage>(BOX, 0, 0, "wf_act_show_message");

        (await SaveMessageAsync(new string('a', 600))).IsSaved.Should().BeTrue();

        box.GetSnapshot().StringParam.Should().Be(new string('a', 400));
    }

    [Fact]
    public async Task Users_by_name_refuses_a_long_name_and_too_many_names_with_Habbos_limits()
    {
        _room.AddBox<WiredSelectorEntitiesByName>(BOX, 0, 0, "wf_slc_users_byname");

        (await SaveNamesAsync(new string('n', 30))).IsSaved.Should().BeTrue();
        var tooLong = await SaveNamesAsync(new string('n', 31));
        tooLong.ErrorKey.Should().Be(WiredSaveErrors.USERNAME_LENGTH);
        tooLong.Parameters.Should().Equal(("max_username_length", "30"));

        (await SaveNamesAsync(Names(20))).IsSaved.Should().BeTrue();
        var tooMany = await SaveNamesAsync(Names(21));
        tooMany.ErrorKey.Should().Be(WiredSaveErrors.USERNAMES_COUNT);
        tooMany.Parameters.Should().Equal(("max_usernames", "20"));
    }

    private static string Names(int count) =>
        string.Join('\t', Enumerable.Range(1, count).Select(i => $"u{i}"));

    private Task<WiredSaveResult> SaveNamesAsync(string names) =>
        _room.SaveWithResultAsync<UpdateSelectorMessage>(BOX, stringParam: names);

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
    [InlineData(50, 10, 51)]
    [InlineData(150, 10, 151)]
    [InlineData(10, 10, 11)]
    [InlineData(10, 11, 11)]
    public async Task A_variable_fx_range_is_saved_with_its_maximum_above_its_minimum(
        int min,
        int max,
        int storedMax
    )
    {
        var box = _room.AddBox<WiredAddonVariableFxHealthPoints>(BOX, 0, 0, "wf_xtra_varfx_hp");
        var intParams = box.GetDefaultIntParams().ToArray();
        intParams[11] = min;
        intParams[13] = max;

        (await _room.SaveWithResultAsync<UpdateAddonMessage>(BOX, intParams: intParams))
            .IsSaved.Should()
            .BeTrue();

        box.GetSnapshot().IntParams[11].Should().Be(min);
        box.GetSnapshot().IntParams[13].Should().Be(storedMax);
    }
}
