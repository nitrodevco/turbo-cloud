using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A variable fx is sent with a colour and width its style offers: one it does not offer becomes
/// the style's default, as the client's editor resolves it (<c>VariableFxEditorMetadata.optionIn</c>,
/// VariableFxEditorState.as:140-142, WIN63-202610201032).
/// </summary>
public sealed class WiredVariableFxLookTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    // A fresh Health Points box: the editor's colour, width and renderer -1 are sent as
    // 1000 / 2 / 10 (official client capture 2026-10-24T12-52-38 and 16-07-38, wired matrix
    // addon.1200.R2/R5).
    [InlineData(0, -1, -1, -1, 1000, 2, 10)]
    // The single heart offers a colour and no width: bronze and -1 stay (official client capture
    // 2026-10-17T00-31-23, config 2147418763).
    [InlineData(3, 100, -1, 11, 100, -1, 11)]
    public async Task A_health_points_fx_is_sent_with_a_look_its_style_offers(
        int style,
        int color,
        int width,
        int renderer,
        int sentColor,
        int sentWidth,
        int sentRenderer
    )
    {
        var fx = _room.AddBox<WiredAddonVariableFxHealthPoints>(BOX, 0, 0, "wf_xtra_varfx_hp");

        // The official server's fresh box (wired matrix addon.1200.R1) with the look under test.
        int[] intParams =
        [
            1,
            2,
            0,
            7,
            0,
            3000,
            style,
            color,
            width,
            renderer,
            0,
            0,
            0,
            100,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
        ];

        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: intParams)).Should().BeTrue();
        await fx.LoadWiredAsync(Ct);

        var config = fx.BuildConfig();

        (config.ColorId, config.WidthId, config.RendererId)
            .Should()
            .Be((sentColor, sentWidth, sentRenderer));
    }
}
