using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Double-clicking a wired box opens its editor for anyone with rights, whatever usage policy its
/// furni definition carries. The test hotel had usage 0 on newer wired definitions (Give and
/// Change Variable, Initiate Transaction, Transaction Failed, Custom Contract), so the use was
/// refused and their editors never opened; Habbo sends every wired box with usage Rights.
/// </summary>
public sealed class WiredBoxUseTests
{
    private const int OWNER = 1;
    private const int BOX = 40;
    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForPlayer(OWNER, 1);

    [Fact]
    public async Task A_box_whose_definition_allows_nobody_still_opens_for_the_owner()
    {
        var box = _room.AddFloorItem(
            BOX,
            3,
            3,
            "wf_act_give_var",
            usagePolicy: FurnitureUsageType.Nobody,
            createLogic: (factory, ctx) => new WiredActionGiveVariable(_room.Grains, factory, ctx)
        );

        await _room.Harness.Room.UseItemByIdAsync(Owner, BOX, Ct);

        box.Logic.GetUsagePolicy().Should().Be(FurnitureUsageType.Controller);
        OpensSentToOwner().Should().ContainSingle().Which.ItemId.Should().Be(BOX);
    }

    private IEnumerable<OpenEventMessageComposer> OpensSentToOwner() =>
        _room
            .Harness.Fakes.Log.Of("SendComposerAsync")
            .Where(c => c.Interface == typeof(IPlayerPresenceGrain) && Equals(c.Key, (long)OWNER))
            .Select(c => c.Args[0])
            .OfType<OpenEventMessageComposer>();
}
