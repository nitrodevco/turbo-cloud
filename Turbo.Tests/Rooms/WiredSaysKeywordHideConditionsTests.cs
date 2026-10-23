using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A "hide" User Says Keyword box hides a line only when its stack's conditions pass (official
/// client video, nitro-tester 2026-10-22 21-10-16: a "match all text" box with hide over
/// "triggering user is on furni"; the line went to the room while its speaker stood beside the
/// furni and came back to the speaker alone, as a whisper, while they stood on it). Ours hid
/// every line the box picked up, so a failing condition still took the line out of the room.
/// </summary>
public sealed class WiredSaysKeywordHideConditionsTests
{
    private const int SPEAKER = 3;
    private const int TRIGGER = 1;
    private const int ON_MAT = 2;
    private const int ELSE_LOG = 4;
    private const int MAT = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSaysKeywordHideConditionsTests()
    {
        _room.Harness.Room.UseEventSystem(new TestEventBus().System);
        _room.Harness.Room.UseCommands(
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );
        _room.AddFloorItem(MAT, 5, 5);
    }

    [Fact]
    public async Task A_line_whose_conditions_fail_goes_to_the_room()
    {
        await BuildAsync();
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("a");

        ToRoom().Select(c => c.Text).Should().Equal("a");
        WhispersTo(100 + SPEAKER).Should().BeEmpty();
    }

    [Fact]
    public async Task A_line_whose_conditions_pass_is_whispered_back_to_its_speaker_alone()
    {
        await BuildAsync();
        _room.Enter(SPEAKER, 5, 5);

        await SayAsync("a");

        ToRoom().Should().BeEmpty();
        WhispersTo(100 + SPEAKER).Select(c => c.Text).Should().Equal("a");
    }

    [Fact]
    public async Task An_else_effect_running_on_failed_conditions_does_not_hide_the_line()
    {
        _room.AddBox<WiredActionNegativeWriteToLogs>(ELSE_LOG, 0, 0, "wf_act_neg_log");
        await BuildAsync();
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("a");

        ToRoom().Select(c => c.Text).Should().Equal("a");
    }

    [Fact]
    public async Task With_the_setting_off_the_box_hides_whatever_its_conditions_decide()
    {
        _room.Harness.Room.UseWiredConfig(
            new WiredConfig { HideKeywordOnlyWhenConditionsPass = false }
        );
        await BuildAsync();
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("a");

        ToRoom().Should().BeEmpty();
        WhispersTo(100 + SPEAKER).Select(c => c.Text).Should().Equal("a");
    }

    private async Task BuildAsync()
    {
        _room.AddBox<WiredTriggerHabboSaysKeyword>(TRIGGER, 0, 0, "wf_trg_says_something");
        _room.AddBox<WiredConditionExecutorOnItem>(ON_MAT, 0, 0, "wf_cnd_trggrer_on_frn");

        // The tester's box: not owner only, match all text, hide.
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [0, 2, 1]))
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                ON_MAT,
                stuffIds: [MAT],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }

    private Task SayAsync(string text) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            _room.ContextFor(SPEAKER),
            RoomChatType.Chat,
            text,
            0,
            -1,
            null,
            Ct
        );

    private IEnumerable<ChatMessageComposer> ToRoom() =>
        _room
            .Harness.Fakes.SentToRoom<ChatMessageComposer>()
            .Where(c => c is not WhisperMessageComposer);

    private IEnumerable<WhisperMessageComposer> WhispersTo(int playerId) =>
        _room.Harness.Fakes.SentToPlayer<WhisperMessageComposer>(playerId);
}
