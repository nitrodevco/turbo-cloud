using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User says keyword" with "hide" ticked, as Habbo runs it (hhus, 2026-10-13, a box set to match
/// all text): the line is not shown to the room; its speaker alone gets it back as a whisper over
/// their own avatar - Chat and Shout alike. The checkbox used to be stored but nothing read it,
/// so every hidden line went to the whole room.
/// </summary>
public sealed class WiredSaysKeywordHideTests
{
    private const int SPEAKER = 3;

    private readonly WiredRoom _room = new(8, 8);

    public WiredSaysKeywordHideTests()
    {
        // The chat path publishes every line and matches commands first, as CommandRoomFixture sets up.
        _room.Harness.Room.UseEventSystem(new TestEventBus().System);
        _room.Harness.Room.UseCommands(
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );
    }

    [Theory]
    [InlineData(RoomChatType.Chat)]
    [InlineData(RoomChatType.Shout)]
    public async Task A_hidden_line_is_whispered_back_to_its_speaker_and_not_shown_to_the_room(
        RoomChatType type
    )
    {
        await AddSaysKeywordAsync(hide: true);
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("parity hello", type);

        ToRoom().Should().BeEmpty();
        var echo = WhispersTo(100 + SPEAKER).Should().ContainSingle().Subject;
        echo.Text.Should().Be("parity hello");
        echo.ObjectId.Value.Should().Be(SPEAKER);
        echo.ReceiverRoomIndex.Should().Be(SPEAKER);
    }

    [Fact]
    public async Task Without_hide_the_line_goes_to_the_room_as_before()
    {
        await AddSaysKeywordAsync(hide: false);
        _room.Enter(SPEAKER, 2, 2);

        await SayAsync("parity hello", RoomChatType.Chat);

        ToRoom().Select(c => c.Text).Should().Equal("parity hello");
        WhispersTo(100 + SPEAKER).Should().BeEmpty();
    }

    private async Task AddSaysKeywordAsync(bool hide)
    {
        _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");

        // Habbo's own box: not owner only, match all text, hide as given.
        (await _room.SaveAsync<UpdateTriggerMessage>(1, intParams: [0, 2, hide ? 1 : 0]))
            .Should()
            .BeTrue();
    }

    private Task SayAsync(string text, RoomChatType type) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            ActionContext.CreateForPlayer(100 + SPEAKER, _room.Harness.Room.RoomId),
            type,
            text,
            0,
            -1,
            null,
            CancellationToken.None
        );

    private IEnumerable<ChatMessageComposer> ToRoom() =>
        _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .SelectMany(c => ((RoomOutboundSnapshot)c.Args[0]!).Composers)
            .OfType<ChatMessageComposer>()
            .Where(c => c is not WhisperMessageComposer);

    private IEnumerable<WhisperMessageComposer> WhispersTo(int playerId) =>
        _room
            .Harness.Fakes.Log.Of("SendComposerAsync")
            .Where(c =>
                c.Key switch
                {
                    PlayerId p => p.Value == playerId,
                    int n => n == playerId,
                    long n => n == playerId,
                    _ => false,
                }
            )
            .Select(c => c.Args[0])
            .OfType<WhisperMessageComposer>();
}
