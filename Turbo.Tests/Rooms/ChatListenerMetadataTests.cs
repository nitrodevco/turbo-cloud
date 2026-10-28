using FluentAssertions;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

public sealed class ChatListenerMetadataTests
{
    [Theory]
    [InlineData("listener", 102)]
    [InlineData("absent-bot", null)]
    public async Task Whisper_metadata_keeps_the_addressed_name_and_speaker_tile_without_changing_delivery(
        string name,
        int? player
    )
    {
        var room = new WiredRoom(8, 8);
        var events = new TestEventBus();
        events.Record<PlayerChatEvent>();
        room.Harness.Room.UseEventSystem(events.System);
        room.Harness.Room.UseCommands(
            new CommandRegistryProvider(new CapturingLogger<ICommandRegistryProvider>())
        );
        room.Enter(1, 3, 4, "speaker");
        room.Enter(2, 2, 2, "listener");
        await room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            room.ContextFor(1),
            RoomChatType.Whisper,
            "private hello",
            0,
            -1,
            name,
            CancellationToken.None
        );
        var heard = events.Of<PlayerChatEvent>().Should().ContainSingle().Subject;
        heard.RecipientName.Should().Be(name);
        (heard.TargetPlayerId?.Value).Should().Be(player);
        (heard.SpeakerX, heard.SpeakerY).Should().Be((3, 4));
        room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(101).Should().ContainSingle();
        room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(102)
            .Count()
            .Should()
            .Be(player is null ? 0 : 1);
    }
}
