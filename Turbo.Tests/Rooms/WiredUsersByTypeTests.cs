using System.Collections;
using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Users by type" with the user types its editor saves (<c>wiredfurni.params.usertype.1</c> /
/// <c>.2</c> / <c>.4</c>: 1 Habbo, 2 pet, 4 bot). The room holds a player and a bot.
/// </summary>
public sealed class WiredUsersByTypeTests
{
    private const int BOX = 2;
    private const int PLAYER = 5;
    private const int BOT = 9;

    private readonly WiredRoom _room = new(8, 8);

    public WiredUsersByTypeTests()
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
    [InlineData(1, new[] { PLAYER })]
    [InlineData(4, new[] { BOT })]
    [InlineData(2, new int[0])]
    public async Task The_box_selects_the_users_of_the_type_ticked(int type, int[] expected)
    {
        var selector = _room.AddBox<WiredSelectorEntitiesByType>(BOX, 0, 0, "wf_slc_users_bytype");
        (
            await _room.SaveAsync<UpdateSelectorMessage>(
                BOX,
                intParams: [type],
                definitionSpecifics: [false, false]
            )
        )
            .Should()
            .BeTrue();

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

        var selected = await selector.SelectAsync(ctx, TestContext.Current.CancellationToken);

        selected.SelectedAvatarIds.Select(x => x.Value).Should().BeEquivalentTo(expected);
    }
}
