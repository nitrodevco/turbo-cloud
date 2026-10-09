using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Action;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// The reward track action types hooked after the first three: a furni moved to another tile
/// (<c>move_item</c>) against one turned in place (<c>rotate_item</c>), a chat line
/// (<c>chat_with_someone</c>), and a badge put on (<c>wear_badge</c>, from the badge-worn fact).
/// The types are the official client's <c>reward_track_tasks_&lt;type&gt;</c> images (JS 88);
/// what each one counts is inference from its name.
/// </summary>
public sealed class RewardTrackHooksTests
{
    private readonly Fakes _fakes = new();

    private static ActionContext Player => ActionContext.CreateForPlayer(1, 7);

    [Fact]
    public void A_move_to_another_tile_is_a_move_and_one_in_place_a_rotation()
    {
        RewardTrackRoomListener
            .ActionTypeOf(Moved(tileChanged: true))
            .Should()
            .Be(RewardTrackActionTypes.MOVE_ITEM);
        RewardTrackRoomListener
            .ActionTypeOf(Moved(tileChanged: false))
            .Should()
            .Be(RewardTrackActionTypes.ROTATE_ITEM);
    }

    [Fact]
    public void A_chat_line_counts_unless_it_was_cancelled()
    {
        var chat = Chat();

        RewardTrackRoomListener
            .ActionTypeOf(chat)
            .Should()
            .Be(RewardTrackActionTypes.CHAT_WITH_SOMEONE);

        chat.Cancel();

        RewardTrackRoomListener.ActionTypeOf(chat).Should().BeNull();
    }

    [Fact]
    public async Task Only_configured_types_reach_the_players_grain()
    {
        var listener = new RewardTrackRoomListener(
            Options.Create(Config(RewardTrackActionTypes.ROTATE_ITEM)),
            _fakes.Create<Orleans.IGrainFactory>(),
            NullLogger<RewardTrackRoomListener>.Instance
        );

        await listener.OnRoomEventAsync(Moved(tileChanged: true), default);
        await listener.OnRoomEventAsync(Moved(tileChanged: false), default);
        await listener.OnRoomEventAsync(Chat(), default);

        _fakes
            .Log.Of("RecordActionAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(RewardTrackActionTypes.ROTATE_ITEM);
    }

    [Fact]
    public void A_badge_put_on_counts_as_wearing_one()
    {
        var listener = new RewardTrackFactListener(
            Options.Create(Config(RewardTrackActionTypes.WEAR_BADGE)),
            _fakes.Create<Orleans.IGrainFactory>(),
            NullLogger<RewardTrackFactListener>.Instance
        );

        listener.OnFactRecorded(
            1,
            new AchievementFact
            {
                Source = AchievementSources.BADGE_WORN,
                OperationId = "op",
                OccurredAtUtc = DateTime.UtcNow,
                Value = "ACH_BasicClub1",
            }
        );

        _fakes
            .Log.Of("RecordActionAsync")
            .Select(x => (x.Args[0], x.Args[1]))
            .Should()
            .Equal(((object?)RewardTrackActionTypes.WEAR_BADGE, (object?)""));
    }

    private static RewardTrackConfig Config(string actionType) =>
        new()
        {
            Tracks =
            [
                new()
                {
                    Id = "t",
                    Tasks =
                    [
                        new()
                        {
                            Id = "task",
                            ActionType = actionType,
                            Levels = [new() { RequiredCount = 1, Points = 10 }],
                        },
                    ],
                },
            ],
        };

    private static RoomItemMovedEvent Moved(bool tileChanged) =>
        new()
        {
            RoomId = 7,
            CausedBy = Player,
            ObjectId = 3,
            PrevIdx = 4,
            TileChanged = tileChanged,
        };

    private static PlayerChatEvent Chat() =>
        new()
        {
            RoomId = 7,
            CausedBy = Player,
            PlayerId = 1,
            ObjectId = 1,
            ChatType = RoomChatType.Chat,
            StyleId = 0,
            TrackingId = 0,
            Gesture = AvatarGestureType.None,
            Text = "hi",
        };
}
