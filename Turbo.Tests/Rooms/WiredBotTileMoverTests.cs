using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Logic.Avatars;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Bot;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A bot on the tile a furni mover would put block K on (official client capture
/// 2026-10-24T20-01-27 / 20-02-14 / 20-03-05, WIN63-202610201032, W1 lab, bot "frank"): Move and
/// Rotate, Move As Group, Furni To Furni and Match Snapshot are blocked, K stays (action.4.B3,
/// action.57.B3, action.34.B3, action.3.B3, 2/2 each, each with a control moving K once the bot
/// was gone); Set Altitude lifts a bot standing on K in the same WiredMovements (action.29.B3,
/// 1/1). K stands on 3,3 and the bot on 4,3.
/// </summary>
public sealed class WiredBotTileMoverTests
{
    private const int CLICK_ME = 20;
    private const int K = 21;
    private const int TARGET = 22;
    private const int BOT = 30;
    private const int BOX = 2;

    private readonly WiredRoom _room = new(10, 10);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData("wf_act_move_rotate")]
    [InlineData("wf_act_move_furni_as_group")]
    [InlineData("wf_act_furni_to_furni")]
    [InlineData("wf_act_match_to_sshot")]
    public async Task A_bot_on_the_target_tile_blocks_the_mover(string mover)
    {
        _room.AddFloorItem(K, mover == "wf_act_match_to_sshot" ? 4 : 3, 3);
        _room.AddFloorItem(TARGET, 4, mover == "wf_act_move_furni_as_group" ? 4 : 3);

        await BuildAsync(() => SaveMoverAsync(mover));

        if (mover == "wf_act_match_to_sshot")
            _room.Map.MoveFloorItem(_room.FloorItem(K), _room.Map.ToIdx(3, 3));

        AddBot(4, 3);

        await ClickAsync();

        At(K).Should().Be((3, 3), "{0} must not put K onto the bot", mover);
        _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .SelectMany(x => x.FloorItems)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Set_altitude_lifts_a_bot_standing_on_the_furni()
    {
        _room.AddFloorItem(K, 4, 3);

        await BuildAsync(async () =>
        {
            _room.AddBox<WiredActionSetAltitude>(BOX, 0, 0, "wf_act_set_altitude");

            return await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [200, 0],
                stuffIds: [K],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            );
        });

        AddBot(4, 3);

        await ClickAsync();

        var movements = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().ToList();

        movements.Should().ContainSingle();
        movements[0].FloorItems.Should().ContainSingle(x => x.ObjectId == K);
        movements[0].Users.Should().ContainSingle(x => x.ObjectId == BOT);
    }

    private Task<bool> SaveMoverAsync(string mover)
    {
        switch (mover)
        {
            case "wf_act_move_rotate":
                _room.AddBox<WiredActionMoveRotateFurni>(BOX, 0, 0, mover);

                return Save(intParams: [5, 0]);
            case "wf_act_move_furni_as_group":
                _room.AddBox<WiredActionMoveAsGroup>(BOX, 0, 0, mover);

                return Save(intParams: [0, 0, -1], secondary: true);
            case "wf_act_furni_to_furni":
                _room.AddBox<WiredActionFurniToFurni>(BOX, 0, 0, mover);

                return Save(intParams: [], secondary: true);
            default:
                _room.AddBox<WiredActionMatchToSnapshot>(BOX, 0, 0, mover);

                return Save(intParams: [0, 0, 1, 0]);
        }
    }

    private Task<bool> Save(int[] intParams, bool secondary = false) =>
        _room.SaveAsync<UpdateActionMessage>(
            BOX,
            intParams: intParams,
            stuffIds: [K],
            stuffIds2: secondary ? [TARGET] : null,
            furniSources: secondary
                ?
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                ]
                :
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
            definitionSpecifics: [0]
        );

    private void AddBot(int x, int y)
    {
        var bot = new RoomBotAvatar
        {
            ObjectId = BOT,
            BotId = BOT,
            OwnerId = 1,
            OwnerName = "owner",
            RoomId = 1,
            Skills = [],
        };

        bot.SetName("frank");
        bot.SetPosition(x, y);
        bot.SetLogic(_room.Harness.Fakes.Create<IRoomBotLogic>(BOT));
        _room.Harness.State.AvatarsByObjectId[BOT] = bot;
        _room.Harness.State.AvatarsByBotId[BOT] = BOT;
        _room.Harness.State.Bots.Add(bot);
        _room.Map.AddAvatar(bot, false);
    }

    private (int, int) At(int id) => (_room.FloorItem(id).X, _room.FloorItem(id).Y);

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
    }

    private async Task BuildAsync(Func<Task<bool>> effect)
    {
        _room.AddFloorItem(CLICK_ME, 9, 0);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
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

        (await effect()).Should().BeTrue();

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

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
