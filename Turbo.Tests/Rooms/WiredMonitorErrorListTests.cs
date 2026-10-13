using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The wired monitor's error list is always the same ten errors, in Habbo's order and with its
/// ids and categories; an unused one has a count of 0 and -1 for its latest occurrence (official
/// client capture of WiredErrorLogs, WIN63-202610121624). The info popup reads
/// <c>wiredmenu.error_info.&lt;id&gt;</c> (AS3 WiredErrorInfoView), so the ids must be Habbo's. A
/// "write to logs" line belongs to the room's log, not to this list.
/// </summary>
public sealed class WiredMonitorErrorListTests
{
    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public void A_quiet_room_lists_all_ten_errors_unused()
    {
        var errors = Wired.GetErrorLogs(_now);

        errors
            .Select(x => (x.ErrorId, x.ErrorName, x.Category))
            .Should()
            .Equal(
                (0, "EXECUTION_CAP", "ERROR"),
                (1, "DELAYED_EVENTS_CAP", "ERROR"),
                (2, "EXECUTOR_OVERLOAD", "ERROR"),
                (3, "MARKED_AS_HEAVY", "WARNING"),
                (4, "KILLED", "ERROR"),
                (5, "RECURSION_TIMEOUT", "ERROR"),
                (6, "TOO_MANY_VARIABLES", "ERROR"),
                (7, "TRANSACTION_FAILURE", "ERROR"),
                (8, "PLACEMENT_FAILURE", "ERROR"),
                (8, "VARIABLE_FX_LIMIT", "ERROR")
            );
        errors.Should().OnlyContain(x => x.ThrowCount == 0 && x.MsSinceLastOccurrence == -1);
    }

    [Fact]
    public async Task A_call_chain_cut_too_deep_counts_as_a_recursion_timeout()
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync();

        await CallTooDeepAsync();
        await CallTooDeepAsync();
        _now += 3_000;

        var errors = Wired.GetErrorLogs(_now);

        errors.Should().HaveCount(10);
        errors[5].ErrorName.Should().Be("RECURSION_TIMEOUT");
        errors[5].ThrowCount.Should().Be(2);
        errors[5].MsSinceLastOccurrence.Should().Be(3_000);
        errors.Where((_, i) => i != 5).Should().OnlyContain(x => x.ThrowCount == 0);

        Wired.ClearErrorLogs();

        Wired
            .GetErrorLogs(_now)
            .Should()
            .HaveCount(10)
            .And.OnlyContain(x => x.ThrowCount == 0 && x.MsSinceLastOccurrence == -1);
    }

    private async Task CallTooDeepAsync()
    {
        await Wired.OnRoomEventAsync(
            new WiredStackCalledEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
                FurniIds = [],
                AvatarIds = [],
                Depth = 1_000,
                IsNegative = false,
            },
            Ct
        );

        await TickAsync();
    }

    private async Task TickAsync()
    {
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }
}
