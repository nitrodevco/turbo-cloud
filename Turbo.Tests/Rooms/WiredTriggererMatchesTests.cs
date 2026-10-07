using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Triggerer matches" with the user types its editor saves (<c>wiredfurni.params.usertype.1</c> /
/// <c>.2</c> / <c>.4</c>: 1 Habbo, 2 pet, 4 bot), checked on what triggered the stack.
/// </summary>
public sealed class WiredTriggererMatchesTests
{
    private const int BOX = 2;
    private const int PLAYER = 5;
    private const int BOT = 9;

    private readonly WiredRoom _room = new(8, 8);
    private WiredConditionExecutorMatches _condition = null!;

    public WiredTriggererMatchesTests()
    {
        _room.Enter(PLAYER, 1, 1);

        var bot = new RoomBotAvatar
        {
            ObjectId = BOT,
            BotId = BOT,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };
        bot.SetPosition(2, 2);
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(_room.Harness.State, "AvatarsByObjectId")!
        )[BOT] = bot;
    }

    [Theory]
    [InlineData(PLAYER, 1, true)]
    [InlineData(PLAYER, 4, false)]
    [InlineData(BOT, 4, true)]
    [InlineData(BOT, 2, false)]
    [InlineData(BOT, 1, false)]
    public async Task The_triggerer_matches_the_user_types_ticked(
        int triggerer,
        int mask,
        bool holds
    )
    {
        _condition = _room.AddBox<WiredConditionExecutorMatches>(
            BOX,
            0,
            0,
            "wf_cnd_triggerer_match"
        );
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                BOX,
                intParams: [mask],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredProcessingContext(_room.Harness.Room)
        {
            Event = new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = Turbo.Primitives.Action.ActionContext.CreateForSystem(1),
                StackIds = [],
            },
            Stack = _room.Harness.Fakes.Create<IWiredStack>(),
        };
        ctx.Selected.SelectedAvatarIds.Add(triggerer);

        _condition.Evaluate(ctx).Should().Be(holds);
    }
}
