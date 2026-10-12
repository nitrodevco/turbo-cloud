using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Double-clicking a wired box flips its state before the editor opens, as the official client
/// capture shows (2026-10-12, a Show Message box used twice): UseFurniture is answered with
/// ObjectDataUpdate "1" and then Open, and the next use with "0" and Open.
/// </summary>
public sealed class WiredUseStateTests
{
    private const int BOX = 7;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Using_a_wired_box_flips_its_state_each_time()
    {
        var room = new WiredRoom();
        var box = room.AddBox<WiredActionShowMessage>(BOX, 3, 3, "wf_act_show_message");
        var owner = ActionContext.CreateForSystem(1);

        box.GetState().Should().Be(0);

        await box.OnUseAsync(owner, 0, Ct);
        box.GetState().Should().Be(1);

        await box.OnUseAsync(owner, 0, Ct);
        box.GetState().Should().Be(0);
    }
}
