using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User says keyword" as its editor saves it: Flash's readIntParamsFromForm sends the checkbox
/// with id 0 ("only the owner"), the match mode, then the checkbox with id 1 ("hide").
/// </summary>
public sealed class WiredSaysKeywordParamsTests
{
    private readonly WiredRoom _room = new(8, 8);

    [Theory]
    [InlineData(new[] { 0, 0, 1 }, true)]
    [InlineData(new[] { 1, 0, 0 }, false)]
    public async Task Hide_is_the_last_param(int[] intParams, bool hides)
    {
        var trigger = _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");

        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: intParams, stringParam: "hi"))
            .Should()
            .BeTrue();

        trigger.ShouldHideMessage().Should().Be(hides);
    }
}
