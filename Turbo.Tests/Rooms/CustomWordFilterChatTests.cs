using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A player's own word filter, as Habbo applies it (official client capture, 2026-10-13, one
/// player filtering "pineapple" while another typed): the server masks the line for the player
/// who set the filter only, each listed word in any case becoming "bobba" - Chat "I like bobba
/// pizza", Shout "bobba shout" for "PINEAPPLE shout", Whisper "secret bobba". The speaker and
/// everyone else see what was typed. Turbo kept the list but sent every line unmasked.
/// </summary>
public sealed class CustomWordFilterChatTests
{
    private const int SPEAKER = 11;
    private const int FILTERING = 12;
    private const int BYSTANDER = 13;

    private readonly WiredRoom _room = new(8, 8);

    public CustomWordFilterChatTests()
    {
        // The chat path publishes every line and matches commands first, as CommandRoomFixture sets up.
        _room.Harness.Room.UseEventSystem(new TestEventBus().System);
        _room.Harness.Room.UseCommands(
            new Turbo.Commands.CommandRegistryProvider(
                new CapturingLogger<Turbo.Primitives.Commands.ICommandRegistryProvider>()
            )
        );

        _room.Enter(SPEAKER, 1, 1, "speaker");
        _room.Enter(FILTERING, 2, 2, "filtering");
        _room.Enter(BYSTANDER, 3, 3, "bystander");
    }

    [Theory]
    [InlineData(RoomChatType.Chat, "I like pineapple pizza", "I like bobba pizza")]
    [InlineData(RoomChatType.Shout, "PINEAPPLE shout", "bobba shout")]
    public async Task The_filtering_player_alone_is_sent_the_line_masked(
        RoomChatType type,
        string typed,
        string masked
    )
    {
        await FilterAsync("pineapple");

        await SayAsync(typed, type);

        var toRoom = RoomSends().Should().ContainSingle().Subject;
        toRoom.ExcludedPlayerIds.Should().Equal((PlayerId)(100 + FILTERING));
        toRoom.Composers.OfType<ChatMessageComposer>().Single().Text.Should().Be(typed);

        var own = _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + FILTERING)
            .Should()
            .ContainSingle()
            .Subject;
        own.GetType().Should().Be(toRoom.Composers[0].GetType());
        own.Text.Should().Be(masked);
        own.ObjectId.Value.Should().Be(SPEAKER);
    }

    [Fact]
    public async Task A_whisper_to_the_filtering_player_is_masked_for_them_and_not_for_its_speaker()
    {
        await FilterAsync("pineapple");

        await SayAsync("secret pineapple", RoomChatType.Whisper, "filtering");

        _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + FILTERING)
            .Select(c => c.Text)
            .Should()
            .Equal("secret bobba");
        _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + SPEAKER)
            .Select(c => c.Text)
            .Should()
            .Equal("secret pineapple");
        _room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + BYSTANDER).Should().BeEmpty();
    }

    [Fact]
    public async Task A_line_without_the_word_and_a_cleared_filter_go_to_the_room_unmasked()
    {
        await FilterAsync("pineapple");
        await SayAsync("plain hello", RoomChatType.Chat);

        await FilterAsync();
        await SayAsync("I like pineapple pizza", RoomChatType.Chat);

        RoomSends().Should().OnlyContain(x => x.ExcludedPlayerIds == null);
        RoomSends()
            .SelectMany(x => x.Composers.OfType<ChatMessageComposer>())
            .Select(c => c.Text)
            .Should()
            .Equal("plain hello", "I like pineapple pizza");
        _room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + FILTERING).Should().BeEmpty();
    }

    [Fact]
    public async Task The_speaker_sees_their_own_line_as_typed_whatever_their_own_filter()
    {
        await _room.Harness.Room.SetPlayerCustomFilterWordsAsync(
            100 + SPEAKER,
            ["pineapple"],
            CancellationToken.None
        );

        await SayAsync("I like pineapple pizza", RoomChatType.Chat);

        RoomSends().Single().ExcludedPlayerIds.Should().BeNull();
        _room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(100 + SPEAKER).Should().BeEmpty();
    }

    [Fact]
    public async Task A_player_entering_brings_the_filter_they_saved()
    {
        _room.Harness.Fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(
                Turbo.Primitives.Players.Snapshots.Permissions.ResolvedPermissionsSnapshot.EMPTY
            );
        _room.Harness.Fakes.Handlers["CreateAvatarFromPlayerSnapshot"] =
            call => new RoomPlayerAvatar
            {
                ObjectId = (RoomObjectId)call.Args[0]!,
                PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
            };
        _room.Harness.Fakes.Handlers["GetCustomFilterWordsAsync"] = _ =>
            Task.FromResult(new List<string> { "pineapple" });

        var arrived = await _room
            .Harness.Module<RoomAvatarModule>()
            .CreateAvatarFromPlayerAsync(
                ActionContext.CreateForPlayer((PlayerId)42, _room.Harness.Room.RoomId),
                new PlayerSummarySnapshot
                {
                    PlayerId = (PlayerId)42,
                    Name = "arrived",
                    Motto = string.Empty,
                    Figure = string.Empty,
                    Gender = default,
                    AchievementScore = 0,
                    BadgesRank = 0,
                    IsOnline = true,
                    CreatedAt = DateTime.UtcNow,
                    LastUpdated = DateTime.UtcNow,
                    RespectPoints = 0,
                    RespectsLeft = 0,
                    PetRespectsLeft = 0,
                    RespectReplenishesLeft = 0,
                },
                RoomEntrySnapshot.Default,
                CancellationToken.None
            );

        await SayAsync("I like pineapple pizza", RoomChatType.Chat);

        RoomSends().Single().ExcludedPlayerIds.Should().Equal((PlayerId)42);
        _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(42)
            .Select(c => c.Text)
            .Should()
            .Equal("I like bobba pizza");
        arrived.Should().NotBeNull();
    }

    /// <summary>What the presence tells the room when the player changes their list.</summary>
    private Task FilterAsync(params string[] words) =>
        _room.Harness.Room.SetPlayerCustomFilterWordsAsync(
            100 + FILTERING,
            [.. words],
            CancellationToken.None
        );

    private Task SayAsync(string text, RoomChatType type, string? recipient = null) =>
        _room.Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            _room.ContextFor(SPEAKER),
            type,
            text,
            0,
            -1,
            recipient,
            CancellationToken.None
        );

    /// <summary>Each publish to the room that carried a chat line.</summary>
    private List<RoomOutboundSnapshot> RoomSends() =>
        [
            .. _room
                .Harness.Fakes.RoomPublishes()
                .Where(x => x.Composers.OfType<ChatMessageComposer>().Any()),
        ];
}
