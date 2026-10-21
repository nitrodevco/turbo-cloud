using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "View full logs" (AS3 <c>WiredGetRoomLogsComposer</c>: page, page size, level, source, text)
/// is answered with a page of the lines "write to logs" wrote (AS3 <c>WiredLogPage</c> /
/// <c>WiredLogEntry</c>): newest first, source 1 (<c>log_source.1</c> "Wired"), the level code the
/// editor saved (<c>log_level.1</c> WARN), and each filter behind its own flag.
/// </summary>
public sealed class WiredRoomLogsTests
{
    private const int CLICK_ME = 23;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredRoomLogsTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 4);
    }

    [Fact]
    public void The_client_request_reads_page_size_filters_and_text()
    {
        var message = PacketHarness.Parse(
            PacketHarness.Incoming("WiredGetRoomLogsMessageEvent"),
            PacketHarness.Payload(w => w.Int(1).Int(50).Int(-1).Int(-1).String(""))
        );

        message
            .Should()
            .BeEquivalentTo(
                new WiredGetRoomLogsMessage
                {
                    Page = 1,
                    PageSize = 50,
                    LogLevelFilter = -1,
                    LogSourceFilter = -1,
                    Query = "",
                }
            );
    }

    [Fact]
    public async Task A_write_to_logs_line_is_served_in_the_room_log_page()
    {
        await BuildAsync("first");
        await ClickAsync();
        await TickAsync(4);
        await SaveLogAsync("second");
        await ClickAsync();
        await TickAsync(4);

        var page = await _room.Harness.Room.GetWiredRoomLogsAsync(
            ActionContext.CreateForSystem(1),
            1,
            50,
            -1,
            -1,
            "",
            Ct
        );

        page.Should().NotBeNull();
        page!.TotalEntries.Should().Be(2);
        page.Entries.Select(x => x.Message).Should().Equal("second", "first");

        var packet = PacketHarness.Encode(new WiredRoomLogsMessageComposer { Page = page });

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredRoomLogsMessageComposer"));
        packet.PopInt().Should().Be(2);
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(50);
        packet.PopInt().Should().Be(2);

        packet.PopLong().Should().Be(page.Entries[0].Id);
        packet.PopByte().Should().Be(1);
        packet.PopByte().Should().Be(1);
        packet.PopString().Should().Be("second");
        packet.PopLong().Should().BeGreaterThan(0);
        packet.PopString().Should().MatchRegex(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d$");

        packet.PopLong();
        packet.PopByte();
        packet.PopByte();
        packet.PopString().Should().Be("first");
        packet.PopLong();
        packet.PopString();

        // No level, source or text filter.
        packet.PopBoolean().Should().BeFalse();
        packet.PopBoolean().Should().BeFalse();
        packet.PopBoolean().Should().BeFalse();
        packet.End.Should().BeTrue();

        var filtered = Wired.GetRoomLogPage(1, 50, 1, 1, "FIR");

        filtered.Entries.Select(x => x.Message).Should().Equal("first");
        filtered.TotalEntries.Should().Be(1);
    }

    private async Task BuildAsync(string text)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionWriteToLogs>(2, 0, 0, "wf_act_log");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        await SaveLogAsync(text);
    }

    private async Task SaveLogAsync(string text)
    {
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: text,
                definitionSpecifics: [2]
            )
        )
            .Should()
            .BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private Task ClickAsync() =>
        _room.FloorItem(CLICK_ME).Logic.OnClickAsync(_room.ContextFor(5), 0, Ct);

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
