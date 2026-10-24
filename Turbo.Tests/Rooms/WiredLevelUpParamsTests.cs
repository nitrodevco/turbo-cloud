using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Level-up system (<c>wf_xtra_var_lvlup_system</c>) params: [sub-variable mask, mode, a, b, c].
/// The editor sends only the mode's ints; Habbo stores 5, padding with 0 and zeroing what the
/// mode does not use, raises a growth factor below 1 and a max level below 2, keeps any mask,
/// and stores a manual table holding a line that is not "level=xp" as empty (official client
/// capture 2026-10-22T22-57-46-click / 22-58-38 / 22-59-07-UpdateAddon).
/// </summary>
public sealed class WiredLevelUpParamsTests
{
    private const int BOX = 1;

    private readonly WiredRoom _room = new(8, 8);

    private readonly WiredAddonVariableLevelUp _box;

    public WiredLevelUpParamsTests() =>
        _box = _room.AddBox<WiredAddonVariableLevelUp>(BOX, 0, 0, "wf_xtra_var_lvlup_system");

    // Wired matrix addon.1001.R4 / addon.1202.R4: linear, step 10, 5 levels. Habbo's level fx
    // showed level 1 at 9 xp, 2 at 10 and 11, 4 at 39, 5 at 40 and 41, and level 3 at 20 and 21
    // between 20 and 30 (official client capture, measured).
    [Theory]
    [InlineData(9, 1, 0, 10, false)]
    [InlineData(10, 2, 10, 20, false)]
    [InlineData(11, 2, 10, 20, false)]
    [InlineData(20, 3, 20, 30, false)]
    [InlineData(21, 3, 20, 30, false)]
    [InlineData(39, 4, 30, 40, false)]
    [InlineData(40, 5, 40, 40, true)]
    [InlineData(41, 5, 40, 40, true)]
    public async Task Linear_levels_start_at_each_step(
        long xp,
        int level,
        long levelStart,
        long nextLevel,
        bool maxed
    )
    {
        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: [255, 1, 10, 5]))
            .Should()
            .BeTrue();
        await _box.LoadWiredAsync(TestContext.Current.CancellationToken);

        _box.TryGetLevelProgress(xp, out var progress).Should().BeTrue();
        progress.Should().Be(new WiredLevelProgress(level, 5, levelStart, nextLevel, maxed));
    }

    [Fact]
    public void A_fresh_box_holds_the_linear_defaults_in_five_ints() =>
        _box.GetDefaultIntParams().Should().Equal(0, 1, 100, 50, 50);

    [Theory]
    [InlineData(new[] { 65, 2, 100, 20, 50 }, new[] { 65, 2, 100, 20, 50 })]
    [InlineData(new[] { 65, 0 }, new[] { 65, 0, 0, 0, 0 })]
    [InlineData(new[] { 65, 1, 100, 50 }, new[] { 65, 1, 100, 50, 0 })]
    [InlineData(new[] { 0, 1, 100, 50, 50 }, new[] { 0, 1, 100, 50, 0 })]
    [InlineData(new[] { 0, 2, 100, 0, 1 }, new[] { 0, 2, 100, 1, 2 })]
    [InlineData(new[] { 512, 1, 100, 50 }, new[] { 512, 1, 100, 50, 0 })]
    public async Task Saves_are_stored_as_habbo_stores_them(int[] sent, int[] stored)
    {
        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: sent)).Should().BeTrue();

        _box.GetSnapshot().IntParams.Should().Equal(stored);
    }

    [Theory]
    [InlineData("5=100", "5=100")]
    [InlineData("5=100\n10=500", "5=100\n10=500")]
    [InlineData("5=100\r10=500", "")]
    [InlineData("1=0\r2=100\r3=300", "")]
    [InlineData("abc", "")]
    public async Task A_manual_table_with_a_line_that_is_not_level_xp_is_stored_empty(
        string sent,
        string stored
    )
    {
        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: [0, 0], stringParam: sent))
            .Should()
            .BeTrue();

        _box.GetSnapshot().StringParam.Should().Be(stored);
    }

    // Wired matrix addon.1001.R1 (wlab4): lvlup [255,1,10,5,0] on room variable "lv", read with a
    // Variable Placeholder. xp 25: level 3, current_xp 25, progress 5, progress_percentage 50,
    // xp_required 10, xp_remaining 5, max_level 5; xp 100: level 5, current_xp 40 (the top level's
    // start), progress, percentage, required and remaining 0; is_maxed printed "null" both times
    // (official client capture 2026-10-24, 18/18).
    [Theory]
    [InlineData(25, "3,25,5,50,10,5,null,5")]
    [InlineData(100, "5,40,0,0,0,0,null,5")]
    public async Task Addon_1001_R1_sub_variables_read_as_habbo_prints_them(int xp, string printed)
    {
        var ct = TestContext.Current.CancellationToken;
        var variable = _room.AddBox<WiredVariableRoom>(10, 0, 0, "wf_var_room");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "lv"
            )
        )
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: [255, 1, 10, 5, 0]))
            .Should()
            .BeTrue();
        await variable.LoadWiredAsync(ct);
        await _box.LoadWiredAsync(ct);

        var key = new WiredVariableKey(
            variable.GetVarSnapshot().VariableId,
            WiredVariableTargetType.Global,
            0
        );
        (
            await variable.SetValueAsync(
                _room.Harness.Fakes.Create<IWiredExecutionContext>(),
                key,
                xp
            )
        )
            .Should()
            .BeTrue();

        var values = new List<string>();

        foreach (var sub in _box.GetSubVariables().Cast<WiredSubVariable>())
        {
            var subKey = key with { VariableId = sub.GetVarSnapshot().VariableId };

            values.Add(
                sub.TryGetValue(subKey, out var value) ? ((int)value).ToString()
                : sub.HoldsNoValueFor(subKey) ? "null"
                : "-"
            );
        }

        string.Join(",", values).Should().Be(printed);
    }
}
