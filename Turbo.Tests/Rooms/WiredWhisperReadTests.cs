using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Snapshots.Settings;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Whispers and Says Keyword, as Habbo has them (official client capture 2026-10-20): a whisper
/// fires the box (5 of 5), to an absent name and to oneself alike, and comes back to its speaker;
/// a whisperer who ticked "Disable whispering to Wired" fires nothing by whispering (2 of 2) while
/// their chat still fires. Turbo ignored every whisper and dropped one to an absent name.
/// </summary>
public sealed class WiredWhisperReadTests
{
    private const int SPEAKER = 11;
    private const int LISTENER = 12;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(8, 8);
    private readonly TestEventBus _bus = new();
    private readonly List<PlayerChatEvent> _said = [];
    private WiredTriggerHabboSaysKeyword _trigger = null!;

    public WiredWhisperReadTests()
    {
        _room.Harness.Room.UseEventSystem(_bus.System);
        _bus.On<PlayerChatEvent>(_said.Add);
        _room.Harness.Room.UseCommands(
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );

        _room.Enter(SPEAKER, 1, 1, "speaker");
        _room.Enter(LISTENER, 2, 2, "listener");
    }

    [Fact]
    public async Task A_whisper_fires_says_keyword()
    {
        await BuildAsync(whisperDisabled: false);

        await SayAsync("lab63kw", RoomChatType.Whisper, "listener");

        (await FiresAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task A_whisper_to_an_absent_name_comes_back_to_its_speaker_and_fires()
    {
        await BuildAsync(whisperDisabled: false);

        await SayAsync("lab63kw", RoomChatType.Whisper, "nobody");

        _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + SPEAKER)
            .Should()
            .ContainSingle();
        _room.Harness.Fakes.SentToPlayer<IComposer>(100 + LISTENER).Should().BeEmpty();
        (await FiresAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task A_whisperer_who_opted_out_fires_nothing_by_whispering_but_still_by_talking()
    {
        await BuildAsync(whisperDisabled: true);

        await SayAsync("lab63kw", RoomChatType.Whisper, "listener");
        (await FiresAsync()).Should().BeFalse();

        await SayAsync("lab63kw", RoomChatType.Chat);
        (await FiresAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task The_whisper_read_notice_goes_to_the_whisperer_only_when_turned_on()
    {
        await BuildAsync(whisperDisabled: false);
        await SayAsync("lab63kw", RoomChatType.Whisper, "listener");
        _room
            .Harness.Fakes.SentToPlayer<NotificationDialogMessageComposer>(100 + SPEAKER)
            .Should()
            .BeEmpty();

        _room.Harness.Room.UseWiredConfig(new WiredConfig { NotifyWhisperRead = true });
        await SayAsync("lab63kw", RoomChatType.Whisper, "listener");

        _room
            .Harness.Fakes.SentToPlayer<NotificationDialogMessageComposer>(100 + SPEAKER)
            .Should()
            .ContainSingle(x =>
                x.NotificationType == "wired.error"
                && x.Parameters["message"] == "${room.error.wired.whisper_read}"
            );
    }

    private Task<bool> FiresAsync() => _trigger.MatchesEventAsync(_said[^1], Ct);

    private async Task BuildAsync(bool whisperDisabled)
    {
        _trigger = _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                intParams: [0, 0, 0],
                stringParam: "lab63kw"
            )
        )
            .Should()
            .BeTrue();
        await _trigger.LoadWiredAsync(Ct);

        _room.Harness.Fakes.Handlers["GetSettingsAsync"] = _ =>
            Task.FromResult(Settings(whisperDisabled));
    }

    private Task SayAsync(string text, RoomChatType type, string? recipient = null) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            _room.ContextFor(SPEAKER),
            type,
            text,
            0,
            -1,
            recipient,
            Ct
        );

    private static PlayerSettingsSnapshot Settings(bool whisperDisabled) =>
        new()
        {
            GenericVolume = 100,
            FurniVolume = 100,
            TraxVolume = 100,
            RoomInvitesIgnored = false,
            RoomCameraFollowDisabled = false,
            UIFlags = default,
            ChatStyleId = 0,
            ChatFontSize = default,
            ChatMode = default,
            ChatBubbleWidth = default,
            ChatScrollSpeed = default,
            WiredMenuButton = false,
            WiredInspectButton = false,
            WiredPlayTestMode = false,
            WiredVariableSyntaxMode = 0,
            WiredWhisperDisabled = whisperDisabled,
            WiredShowAllNotifications = false,
            WiredUIStyle = string.Empty,
            OnlineIndicatorPreference = 0,
            HomeRoomId = -1,
            NavigatorWindowX = 0,
            NavigatorWindowY = 0,
            NavigatorWindowWidth = 0,
            NavigatorWindowHeight = 0,
            NavigatorLeftPaneHidden = false,
            NavigatorResultsMode = default,
            FriendRequestsDisabled = false,
            ProfileHidden = false,
        };
}
