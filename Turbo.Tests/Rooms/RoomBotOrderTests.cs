using FluentAssertions;
using Turbo.Primitives.Bots;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The hotel API's bots (<c>IHotelBots</c>) tell a placed bot what to do through
/// <c>IRoomGrain.OrderBotAsync</c>, for plugins such as an AI bot that answers players. An order
/// goes through what a wired bot action goes through (the ten-lines-in-five-seconds limit), a walk
/// ends the follow it was on, and a bot only gestures what a bot can.
/// </summary>
public sealed class RoomBotOrderTests
{
    private const int BOT = 20;
    private const int USER = 5;

    private readonly WiredRoom _room = new(12, 12);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_said_line_is_held_to_ten_in_five_seconds_as_a_wired_bot_line_is()
    {
        AddBot(BOT, "Ada", 1, 1);

        var said = 0;

        for (var i = 0; i < 12; i++)
            if (await _room.Harness.Room.OrderBotAsync(BOT, new BotSayOrder("hi", false), Ct))
                said++;

        said.Should().Be(10);
        _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Should().HaveCount(10);
        _room.Clock.AdvanceMs(5_000);
        (await _room.Harness.Room.OrderBotAsync(BOT, new BotSayOrder("hi", false), Ct))
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task A_whisper_goes_to_a_player_in_the_room_and_not_to_one_who_is_not()
    {
        AddBot(BOT, "Ada", 1, 1);
        _room.Enter(USER, 4, 4);

        (await _room.Harness.Room.OrderBotAsync(BOT, new BotWhisperOrder(100 + USER, "psst"), Ct))
            .Should()
            .BeTrue();
        (await _room.Harness.Room.OrderBotAsync(BOT, new BotWhisperOrder(999, "psst"), Ct))
            .Should()
            .BeFalse();
        _room.Harness.Fakes.SentToRoom<ChatMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task A_walk_ends_the_follow_the_bot_was_on_and_heads_for_the_tile()
    {
        var bot = AddBot(BOT, "Ada", 1, 1);
        bot.FollowObjectId = USER;

        (await _room.Harness.Room.OrderBotAsync(BOT, new BotWalkOrder(6, 6), Ct)).Should().BeTrue();

        bot.FollowObjectId.Should().Be(-1);
        bot.GoalTileId.Should().Be(_room.Map.ToIdx(6, 6));
    }

    [Theory]
    [InlineData(AvatarExpressionType.Wave, true)]
    [InlineData(AvatarExpressionType.Jump, true)]
    [InlineData(AvatarExpressionType.Idle, false)]
    [InlineData(AvatarExpressionType.Respect, false)]
    public async Task A_bot_makes_a_players_gestures_but_neither_sleeps_nor_respects(
        AvatarExpressionType gesture,
        bool made
    )
    {
        AddBot(BOT, "Ada", 1, 1);

        (await _room.Harness.Room.OrderBotAsync(BOT, new BotGestureOrder(gesture), Ct))
            .Should()
            .Be(made);
        _room
            .Harness.Fakes.SentToRoom<ExpressionMessageComposer>()
            .Should()
            .HaveCount(made ? 1 : 0);
    }

    [Fact]
    public async Task An_order_for_a_bot_that_is_not_in_the_room_does_nothing()
    {
        AddBot(BOT, "Ada", 1, 1);

        (await _room.Harness.Room.OrderBotAsync(BOT + 1, new BotSayOrder("hi", false), Ct))
            .Should()
            .BeFalse();
        (await _room.Harness.Room.GetPlacedBotsAsync(Ct))
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<Turbo.Primitives.Bots.Snapshots.BotSnapshot>(x =>
                x.Id == BOT && x.Name == "Ada" && x.X == 1 && x.Y == 1
            );
    }

    private RoomBotAvatar AddBot(int id, string name, int x, int y)
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = id,
            BotId = id,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };

        bot.SetName(name);
        bot.SetPosition(x, y);
        bot.SetLogic(_room.Harness.Fakes.Create<IRoomBotLogic>(id));
        _room.Harness.State.AvatarsByObjectId[id] = bot;
        _room.Harness.State.AvatarsByBotId[id] = id;
        _room.Harness.State.Bots.Add(bot);
        _room.Map.AddAvatar(bot, false);

        return bot;
    }
}
