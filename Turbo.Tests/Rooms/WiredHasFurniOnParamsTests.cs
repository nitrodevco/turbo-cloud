using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Furni Has Furni On (<c>wf_cnd_has_furni_on</c>) keeps one int, "all the selected furni"
/// (<c>requireall.0</c> / <c>.1</c>). Habbo stores a save of 2 as 1 rather than refusing it
/// (official client capture 2026-10-24T07-37-52, WIN63-202610201032, 1/1).
/// </summary>
public sealed class WiredHasFurniOnParamsTests
{
    private readonly WiredRoom _room = new(8, 8);

    // condition.7.E9b; a negative value stored as 0 is inferred from the same clamp.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(-1, 0)]
    public async Task Condition7E9b_AnOutOfRangeModeIsStoredClamped(int sent, int stored)
    {
        var box = _room.AddBox<WiredConditionItemHasItems>(1, 0, 0, "wf_cnd_has_furni_on");

        (await _room.SaveAsync<UpdateConditionMessage>(1, intParams: [sent])).Should().BeTrue();

        box.GetSnapshot().IntParams.Should().Equal(stored);
    }
}
