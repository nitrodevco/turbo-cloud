using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Filter To X Furni" keeping one of four selected lamps: Habbo picks one at random on every
/// firing, not the first one (official client capture 2026-10-16, Furni In Area over four lamps,
/// Filter To 1 Furni, Toggle: 15 clicks toggled 135 x5, 123 x4, 133 x3, 129 x3). Ours kept the
/// same lamp every time.
/// </summary>
public sealed class WiredSelectorFilterRandomTests
{
    private const int FILTER = 11;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filter_to_one_furni_picks_a_random_one_each_firing()
    {
        var filter = _room.AddBox<WiredAddonFurniSelectorFilter>(
            FILTER,
            0,
            0,
            "wf_xtra_filter_furni"
        );

        (await _room.SaveAsync<UpdateAddonMessage>(FILTER, intParams: [1, 0, 1])).Should().BeTrue();

        var kept = new List<int>();

        for (var i = 0; i < 40; i++)
        {
            var ctx = new WiredProcessingContext(_room.Harness.Room)
            {
                Event = new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [],
                },
                Stack = _room.Harness.Fakes.Create<IWiredStack>(),
            };
            ctx.SelectorPool.SelectedFurniIds.UnionWith([30, 31, 32, 33]);

            await filter.MutatePolicyAsync(ctx, Ct);

            ctx.SelectorPool.SelectedFurniIds.Should().ContainSingle();
            kept.Add(ctx.SelectorPool.SelectedFurniIds.Single());
        }

        kept.Distinct().Should().HaveCountGreaterThan(1, "every firing picks again at random");
        kept.Should().OnlyContain(id => id >= 30 && id <= 33);
    }
}
