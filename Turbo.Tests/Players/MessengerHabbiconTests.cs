using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Players.Configuration;
using Turbo.Players.Messenger;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// A Habbicon sent in a conversation: the messenger's picker sends <c>SendHabbicon</c> (chat id,
/// Habbicon, confirmation id; <c>MainView.onHabbiconSelected</c>, messenger/MainView.as:1075),
/// and the console message carries content type 1 and the Habbicon instead of type 0 and a text
/// (<c>incoming/friendlist/_-W1u.parse</c>, WIN63-202610131318). It is stored, confirmed and
/// kept in the history like a text; a Habbicon the sender does not have is not sent.
/// </summary>
public sealed class MessengerHabbiconTests : IDisposable
{
    private const int HABBICON = 61;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();

    public MessengerHabbiconTests()
    {
        foreach (var id in new[] { 1, 2 })
            _db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = $"friend{id}",
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_habbicon_to_a_friend_is_stored_confirmed_and_kept_in_the_history()
    {
        var grain = Messenger();

        (await grain.SendHabbiconAsync(2, HABBICON, 5, "friend1", "hd-180-1", Ct))
            .Should()
            .BeNull();

        var confirmation = Sent<NewConsoleMessageMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;
        (confirmation.ChatId, confirmation.HabbiconId, confirmation.ConfirmationId)
            .Should()
            .Be((2, HABBICON, 5));
        (await grain.GetMessageHistoryAsync(2, string.Empty, Ct))
            .Should()
            .ContainSingle()
            .Which.HabbiconId.Should()
            .Be(HABBICON);
    }

    [Fact]
    public async Task An_online_friend_is_handed_the_habbicon()
    {
        _fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(true);

        await Messenger().SendHabbiconAsync(2, HABBICON, 5, "friend1", "hd-180-1", Ct);

        _fakes.Log.Of("ReceiveHabbiconAsync").Single().Args[1].Should().Be(HABBICON);
        _fakes.Log.Of("ReceiveMessageAsync").Should().BeEmpty();
    }

    [Fact]
    public void The_console_message_carries_the_habbicon_as_content_type_1()
    {
        var composer = new NewConsoleMessageMessageComposer
        {
            ChatId = 2,
            Message = string.Empty,
            SecondsSinceSent = 0,
            MessageId = "9",
            ConfirmationId = 0,
            SenderId = 1,
            SenderName = "a",
            SenderFigure = "f",
            HabbiconId = HABBICON,
        };

        var packet = PacketHarness.Encode(composer);

        // chat id, type 1, the Habbicon, then the rest as for a text.
        (packet.PopInt(), packet.PopInt(), packet.PopInt())
            .Should()
            .Be((2, 1, HABBICON));
        var text = PacketHarness.Encode(composer with { HabbiconId = 0, Message = "hi" });
        (text.PopInt(), text.PopInt(), text.PopString()).Should().Be((2, 0, "hi"));
    }

    [Fact]
    public async Task A_habbicon_the_sender_does_not_have_is_not_sent()
    {
        var service = Service();

        (await service.SendHabbiconAsync(1, 2, HABBICON, 5, Ct)).Should().BeNull();

        _fakes.Log.Of("SendHabbiconAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task An_owned_habbicon_is_sent_and_counted_as_used()
    {
        _fakes.Handlers["CanUseAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["GetSummaryAsync"] = _ => Task.FromResult(Summary());
        var service = Service();

        (await service.SendHabbiconAsync(1, 2, HABBICON, 5, Ct)).Should().BeNull();

        _fakes
            .Log.Of("SendHabbiconAsync")
            .Single()
            .Args.Take(3)
            .Should()
            .Equal((PlayerId)2, HABBICON, 5);
        _fakes.Log.Of("NoteUsedAsync").Single().Args[0].Should().Be(HABBICON);
    }

    [Fact]
    public async Task A_refused_habbicon_is_answered_with_the_error_and_not_counted()
    {
        _fakes.Handlers["CanUseAsync"] = _ => Task.FromResult(true);
        _fakes.Handlers["GetSummaryAsync"] = _ => Task.FromResult(Summary());
        _fakes.Handlers["SendHabbiconAsync"] = _ =>
            Task.FromResult<InstantMessageErrorCodeType?>(InstantMessageErrorCodeType.NotFriend);

        var reply = await Service().SendHabbiconAsync(1, 2, HABBICON, 5, Ct);

        reply
            .Should()
            .BeOfType<InstantMessageErrorMessageComposer>()
            .Which.ErrorCode.Should()
            .Be(InstantMessageErrorCodeType.NotFriend);
        _fakes.Log.Of("NoteUsedAsync").Should().BeEmpty();
    }

    private IPlayerMessengerGrain Messenger()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.Messenger.PlayerMessengerGrain",
            _fakes,
            _db,
            playerId: 1
        );
        RoomHarness.SetField(
            grain,
            "_playerConfig",
            new PlayerConfig { MessengerHistoryPageSize = 10 }
        );
        var state = grain
            .GetType()
            .GetField(
                "_state",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )!
            .GetValue(grain)!;
        var friends =
            (Dictionary<PlayerId, MessengerFriendDto>)
                state.GetType().GetProperty("Friends")!.GetValue(state)!;
        friends[2] = new MessengerFriendDto { PlayerId = 2, Name = "friend2" };

        return (IPlayerMessengerGrain)grain;
    }

    private MessengerService Service() =>
        new(
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IRoomService>(),
            NullLogger<IMessengerService>.Instance
        );

    private static PlayerSummarySnapshot Summary() =>
        new()
        {
            PlayerId = 1,
            Name = "friend1",
            Motto = string.Empty,
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            AchievementScore = 0,
            BadgesRank = 0,
            IsOnline = true,
            CreatedAt = DateTime.UtcNow,
            LastUpdated = DateTime.UtcNow,
            RespectPoints = 0,
            RespectsLeft = 0,
            PetRespectsLeft = 0,
            RespectReplenishesLeft = 0,
        };

    private IEnumerable<T> Sent<T>()
        where T : IComposer =>
        _fakes
            .Log.Of("SendComposerAsync")
            .SelectMany(x =>
                x.Args[0] switch
                {
                    IReadOnlyList<IComposer> many => many,
                    IComposer one => [one],
                    _ => [],
                }
            )
            .OfType<T>();
}
