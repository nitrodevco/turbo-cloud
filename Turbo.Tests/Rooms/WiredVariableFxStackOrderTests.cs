using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// WIN63-202610121624 gives every variable fx an "Order in FX stack" (Discord 12/10/2026 img4:
/// "Higher values place this FX lower in the object's FX stack."): the editor writes param 21 as
/// the category's slot (0 without one) and 22 as the order (<c>variablefx/_-j10.writeIntParams</c>),
/// and the renderer reads it from the config's extra as <c>stack_order</c>
/// (<c>VariableFxConfigData.stackOrder</c>, used by <c>VariableFxStackController</c>).
/// </summary>
public sealed class WiredVariableFxStackOrderTests
{
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_stack_order_reaches_the_renderer()
    {
        var fx = _room.AddBox<WiredAddonVariableFxHealthPoints>(BOX, 0, 0, "wf_xtra_var_fx_health");
        var intParams = fx.GetDefaultIntParams();

        intParams.Should().HaveCount(23, "the editor sends 23 int params");
        intParams[22] = 3;

        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: [.. intParams]))
            .Should()
            .BeTrue();
        await fx.LoadWiredAsync(Ct);

        fx.BuildConfig().Extra.Should().Contain("stack_order", "3");
    }

    [Fact]
    public async Task A_box_saved_before_the_stack_order_keeps_its_settings()
    {
        var fx = _room.AddBox<WiredAddonVariableFxHealthPoints>(BOX, 0, 0, "wf_xtra_var_fx_health");
        var intParams = fx.GetDefaultIntParams().Take(21).ToArray();

        // Segments, the last param the 20260916 editor wrote.
        intParams[20] = 4;

        (await _room.SaveAsync<UpdateAddonMessage>(BOX, intParams: intParams)).Should().BeTrue();
        await fx.LoadWiredAsync(Ct);

        fx.BuildConfig().Extra.Should().NotContainKey("stack_order");
    }
}
