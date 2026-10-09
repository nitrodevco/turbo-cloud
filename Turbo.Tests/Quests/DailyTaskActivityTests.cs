using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// A find task counts the double-click itself (quests.daily.FINDBBQ.hint: "find the BBQ and
/// double-click on it"), so the room hands every player's use to that player's daily tasks with
/// the furni's name, whether or not the player may change it.
/// </summary>
public sealed class DailyTaskActivityTests
{
    private const int PLAYER = 101;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_players_double_click_is_counted_with_the_furnis_name()
    {
        var harness = new RoomHarness(5, 5);
        var item = harness.CreateFloorItem(70, 1, 1, Altitude.FromInt(0), name: "bbq_grill");
        harness.AddToRoom(item);

        await harness.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(PLAYER, 1), 70, Ct);

        var call = harness.Fakes.Log.Of("RecordActivityAsync").Should().ContainSingle().Subject;
        call.Key.Should().Be((long)PLAYER);
        call.Args[0].Should().Be(DailyTaskActivity.FurniUse);
        call.Args[1].Should().Be("bbq_grill");
    }

    [Fact]
    public async Task A_use_by_the_room_itself_is_not_counted()
    {
        var harness = new RoomHarness(5, 5);
        var item = harness.CreateFloorItem(70, 1, 1, Altitude.FromInt(0), name: "bbq_grill");
        harness.AddToRoom(item);

        await harness.Room.UseItemByIdAsync(ActionContext.CreateForSystem(1), 70, Ct);

        harness.Fakes.Log.Of("RecordActivityAsync").Should().BeEmpty();
    }
}
