using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
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
}
