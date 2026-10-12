using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIN63-202610121624 replaced the 0-20 pulse delay slider with <c>ActionDelaySection</c>:
/// "Standard (per 0.5s)" 0 to 120 pulses, or "Precise (per 0.05s)" 0 to 1200 steps sent as minus
/// the milliseconds (<c>encodedDelay = -(steps * 50)</c>). Wired Faculty 12/10/2026: "More precise
/// delays (0.05s) and longer delays (60s) in wired effects".
/// </summary>
public sealed class WiredActionDelayTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    // Standard: pulses of half a second, now up to 120 (60 s).
    [InlineData(4, 2_000)]
    [InlineData(120, 60_000)]
    [InlineData(500, 60_000)]
    // Precise: minus the milliseconds, in steps of 50 ms (Discord img2: 38.8 s).
    [InlineData(-50, 50)]
    [InlineData(-38_800, 38_800)]
    [InlineData(-1_275, 1_250)]
    [InlineData(-90_000, 60_000)]
    public async Task A_saved_delay_waits_what_the_editor_showed(int encodedDelay, long expectedMs)
    {
        var action = _room.AddBox<WiredActionToggleItemState>(BOX, 0, 0, "wf_act_toggle_state");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0],
                definitionSpecifics: [encodedDelay]
            )
        )
            .Should()
            .BeTrue();
        await action.LoadWiredAsync(Ct);

        ((long)action.GetDelayMs()).Should().Be(expectedMs);
    }
}
