using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Achievement Enabler add-on (<c>wf_xtra_achievement_enabler</c>, Flash
/// <c>AddonCodes.ACHIEVEMENT_ENABLER</c> 2001) and Progress Achievement effect
/// (<c>wf_act_progress_ach</c>, <c>ActionTypeCodes.PROGRESS_ACHIEVEMENT</c> 51), whose furni
/// Habbo published on 2026-10-09 (revision 75953). The editors are in the 2026-09-16 AS3 and
/// the 2026-10-09 JS client; the room had neither box, so both furni stood as plain furniture.
/// </summary>
public sealed class WiredProgressAchievementTests
{
    private const int USER = 5;
    private const int PLAYER = 100 + USER;
    private const int ENABLER = 1;
    private const int TRIGGER = 2;
    private const int EFFECT = 3;
    private const int ACHIEVEMENT_ID = 77;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task The_room_sends_the_enabled_achievements_and_tells_whoever_walks_in()
    {
        await PlaceEnablerAsync("Lap\r\n  Goal \n\nLap\n");

        SentToRoom<WiredEnvironmentMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.EnabledAchievements.Should()
            .Equal("Lap", "Goal");

        await Wired.OnRoomEventAsync(
            new PlayerEnterEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForPlayer((PlayerId)PLAYER, (RoomId)1),
                PlayerId = (PlayerId)PLAYER,
            },
            Ct
        );

        var sent = SentTo<WiredEnvironmentMessageComposer>(PLAYER).Should().ContainSingle().Subject;
        sent.HasClickUserWired.Should().BeFalse();
        sent.EnabledAchievements.Should().Equal("Lap", "Goal");
    }

    [Fact]
    public async Task Only_an_enabled_achievement_can_be_saved()
    {
        await PlaceEnablerAsync("Lap");
        _room.AddBox<WiredActionProgressAchievement>(EFFECT, 0, 0, "wf_act_progress_ach");

        var refused = await _room.SaveWithResultAsync<UpdateActionMessage>(
            EFFECT,
            intParams: [1, 0, 5, 1],
            stringParam: "Goal"
        );

        refused.IsSaved.Should().BeFalse();
        refused.ErrorKey.Should().Be(WiredSaveErrors.ACHIEVEMENT_NOT_ALLOWED);
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [1, 0, 5, 1],
                stringParam: "Lap"
            )
        )
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(1, 5, 8L)]
    [InlineData(0, 10, 10L)]
    [InlineData(0, 2, null)]
    public async Task The_triggering_user_s_achievement_is_added_to_or_set(
        int mode,
        int value,
        long? advancedTo
    )
    {
        var advanced = new List<(int Id, long Progress)>();
        _room.Harness.Fakes.Handlers[nameof(IPlayerAchievementGrain.GetAchievementsAsync)] = call =>
            call.Key is long key && key == PLAYER
                ? Task.FromResult(
                    ImmutableArray.Create(
                        Achievement(11, "ACH_Login3", 50),
                        Achievement(ACHIEVEMENT_ID, "ACH_WF_Lap1", 3)
                    )
                )
                : Task.FromResult(ImmutableArray<AchievementSnapshot>.Empty);
        _room.Harness.Fakes.Handlers[nameof(IPlayerAchievementGrain.AdvanceAsync)] = call =>
        {
            advanced.Add(((int)call.Args[0]!, (long)call.Args[1]!));

            return Task.CompletedTask;
        };
        _room.Enter(USER, 1, 1);
        _room.Enter(6, 3, 3);
        await PlaceEnablerAsync("Lap");
        _room.AddBox<WiredTriggerClickUser>(TRIGGER, 0, 0, "wf_trg_click_user");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [0, 0])).Should().BeTrue();
        _room.AddBox<WiredActionProgressAchievement>(EFFECT, 0, 0, "wf_act_progress_ach");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [mode, 0, value, 1],
                stringParam: "Lap"
            )
        )
            .Should()
            .BeTrue();
        await RebuildAsync(0, 0);

        await _room.Harness.Room.WiredClickAvatarAsync(
            ActionContext.CreateForPlayer((PlayerId)PLAYER, (RoomId)1),
            6,
            Ct
        );

        for (var i = 0; i < 3; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }

        if (advancedTo is { } progress)
            advanced.Should().Equal((ACHIEVEMENT_ID, progress));
        else
            advanced.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ACH_WF_Lap3", "WF_Lap")]
    [InlineData("ACH_Login10", "Login")]
    [InlineData("WF_Goal", "WF_Goal")]
    public void A_badge_names_its_achievement_as_the_client_does(string badgeId, string code) =>
        WiredActionProgressAchievement.CodeOf(badgeId).Should().Be(code);

    private static AchievementSnapshot Achievement(int id, string badgeId, int progress) =>
        new()
        {
            AchievementId = id,
            Level = 1,
            BadgeId = badgeId,
            ScoreAtStartOfLevel = 0,
            ScoreLimitTotal = 100,
            LevelRewardPoints = 0,
            LevelRewardPointType = 0,
            CurrentPointsTotal = progress,
            FinalLevel = false,
            Category = "wired_games",
            SubCategory = "",
            LevelCount = 5,
            DisplayMethod = 0,
            State = default(AchievementState),
        };

    private async Task PlaceEnablerAsync(string names)
    {
        _room.AddBox<WiredAddonAchievementEnabler>(ENABLER, 6, 6, "wf_xtra_achievement_enabler");
        (await _room.SaveAsync<UpdateAddonMessage>(ENABLER, stringParam: names)).Should().BeTrue();
        await RebuildAsync(6, 6);
    }

    private async Task RebuildAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
    }

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();

    private IEnumerable<T> SentTo<T>(long playerId) =>
        _room
            .Harness.Fakes.Log.Calls.Where(x =>
                x.Method == "SendComposerAsync" && x.Key is long key && key == playerId
            )
            .SelectMany(x =>
                x.Args.OfType<IComposer>()
                    .Concat(x.Args.OfType<IEnumerable<IComposer>>().SelectMany(c => c))
            )
            .OfType<T>();
}
