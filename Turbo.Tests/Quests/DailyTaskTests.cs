using FluentAssertions;
using Turbo.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Quests.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Quests;

/// <summary>
/// A player's daily tasks as the client's DailyTasksController sees them: the day's list
/// (DailyTasksActiveList), each step and state change (DailyTasksTaskUpdate, status 0 active,
/// 1 completed, 2 claimed), a bonus task once the others are done (DailyTasksTasksAdded), and a
/// completed task from an earlier day kept claimable with negative seconds left.
/// </summary>
public sealed class DailyTaskTests : IDisposable
{
    private const int EXPLORE = 1;
    private const int FIND_BBQ = 2;
    private const int BONUS = 3;
    private const int DISABLED = 4;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly ManualTimeProvider _clock = new(
        new DateTimeOffset(2026, 10, 9, 20, 0, 0, TimeSpan.Zero)
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DailyTaskTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = 1,
                Name = "daily-tasks",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _db.Insert(Definition(EXPLORE, "1734011389107_G", DailyTaskTypes.EXPLORE, "", 2, 10));
        _db.Insert(
            Definition(FIND_BBQ, "1734011773612_G", DailyTaskTypes.FIND_FURNI, "bbq_grill", 1, 20)
        );
        var bonus = Definition(BONUS, "bonus_G", DailyTaskTypes.FIND_FURNI, "bbq_grill", 1, 5);
        bonus.IsBonus = true;
        _db.Insert(bonus);
        var disabled = Definition(
            DISABLED,
            "1736419911400_G",
            DailyTaskTypes.FIND_FURNI,
            "",
            1,
            20
        );
        disabled.Enabled = false;
        _db.Insert(disabled);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Asking_gives_the_days_regular_tasks_with_the_time_to_the_reset()
    {
        await Grain().SendTasksAsync(Ct);

        var list = Single<DailyTasksActiveListMessageComposer>().Tasks;

        list.Select(x => (x.TaskCode, x.QuestTypeCode, x.IsBonus, x.RequiredRepeats, x.Status))
            .Should()
            .BeEquivalentTo([
                ("1734011389107_G", DailyTaskTypes.EXPLORE, false, 2, DailyTaskStatus.Active),
                ("1734011773612_G", DailyTaskTypes.FIND_FURNI, false, 1, DailyTaskStatus.Active),
            ]);
        list.Should().OnlyContain(x => x.SecondsLeft == 4 * 3600);
        list.Single(x => x.TaskCode == "1734011773612_G")
            .Rewards.Should()
            .Equal(
                new DailyTaskRewardSnapshot
                {
                    ProductType = ProductDisplayType.ActivityPoints,
                    RewardTypeId = "0",
                    ExtraParams = "",
                    Amount = 20,
                }
            );
    }

    [Fact]
    public async Task Asking_again_the_same_day_keeps_the_same_tasks()
    {
        await Grain().SendTasksAsync(Ct);
        await Grain().SendTasksAsync(Ct);

        var lists = Sent<DailyTasksActiveListMessageComposer>().ToList();

        lists.Should().HaveCount(2);
        lists[1].Tasks.Select(x => x.TaskId).Should().Equal(lists[0].Tasks.Select(x => x.TaskId));
    }

    [Fact]
    public async Task Each_other_players_room_counts_once_and_the_last_completes_the_task()
    {
        var grain = Grain();

        await grain.RecordActivityAsync(DailyTaskActivity.RoomVisit, "7", Ct);
        await grain.RecordActivityAsync(DailyTaskActivity.RoomVisit, "7", Ct);
        await grain.RecordActivityAsync(DailyTaskActivity.RoomVisit, "8", Ct);

        Sent<DailyTasksTaskUpdateMessageComposer>()
            .Select(x => (x.Repeats, x.Status))
            .Should()
            .Equal((1, DailyTaskStatus.Active), (2, DailyTaskStatus.Completed));
    }

    [Fact]
    public async Task Double_clicking_a_target_furni_completes_a_find_task_and_others_do_not()
    {
        var grain = Grain();

        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "chair_basic", Ct);
        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "BBQ_GRILL", Ct);

        Sent<DailyTasksTaskUpdateMessageComposer>()
            .Select(x => (x.Repeats, x.Status))
            .Should()
            .Equal((1, DailyTaskStatus.Completed));
    }

    [Fact]
    public async Task Completing_every_regular_task_adds_the_bonus_task_once()
    {
        var grain = Grain();

        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "bbq_grill", Ct);
        Sent<DailyTasksTasksAddedMessageComposer>().Should().BeEmpty();

        await grain.RecordActivityAsync(DailyTaskActivity.RoomVisit, "7", Ct);
        await grain.RecordActivityAsync(DailyTaskActivity.RoomVisit, "8", Ct);
        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "bbq_grill", Ct);

        var added = Single<DailyTasksTasksAddedMessageComposer>().Tasks;
        added.Select(x => (x.TaskCode, x.IsBonus)).Should().Equal(("bonus_G", true));
    }

    [Fact]
    public async Task Claiming_credits_the_reward_once_and_marks_the_task_claimed()
    {
        var grain = Grain();
        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "bbq_grill", Ct);
        var taskId = Single<DailyTasksTaskUpdateMessageComposer>().TaskId;

        (await grain.ClaimAsync(taskId, Ct)).Should().BeTrue();
        (await grain.ClaimAsync(taskId, Ct)).Should().BeFalse();

        var credit = _fakes.Log.Of("CreditAsync").Should().ContainSingle().Subject;
        credit.Args[0].Should().Be(CurrencyKind.ActivityPoints(0));
        credit.Args[1].Should().Be(20);
        credit.Args[2].Should().Be($"dailytask:{taskId}");
        Sent<DailyTasksTaskUpdateMessageComposer>()
            .Last()
            .Status.Should()
            .Be(DailyTaskStatus.Claimed);
    }

    [Fact]
    public async Task A_club_member_gets_double_duckets()
    {
        _fakes.Handlers["HasActiveAsync"] = _ => Task.FromResult(true);
        var grain = Grain();
        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "bbq_grill", Ct);

        await grain.ClaimAsync(Single<DailyTasksTaskUpdateMessageComposer>().TaskId, Ct);

        _fakes.Log.Of("CreditAsync").Single().Args[1].Should().Be(40);
    }

    [Fact]
    public async Task An_active_task_cannot_be_claimed()
    {
        var grain = Grain();
        await grain.SendTasksAsync(Ct);
        var task = Single<DailyTasksActiveListMessageComposer>().Tasks[0];

        (await grain.ClaimAsync(task.TaskId, Ct)).Should().BeFalse();
        _fakes.Log.Of("CreditAsync").Should().BeEmpty();
    }

    [Fact]
    public async Task After_the_reset_new_tasks_come_and_a_completed_one_stays_claimable()
    {
        var grain = Grain();
        await grain.RecordActivityAsync(DailyTaskActivity.FurniUse, "bbq_grill", Ct);
        var completed = Single<DailyTasksTaskUpdateMessageComposer>().TaskId;

        _clock.Advance(TimeSpan.FromHours(5));
        await grain.SendTasksAsync(Ct);

        var list = Single<DailyTasksActiveListMessageComposer>().Tasks;
        list.Single(x => x.TaskId == completed)
            .Should()
            .Match<DailyTaskSnapshot>(x =>
                x.Status == DailyTaskStatus.Completed && x.SecondsLeft == -1
            );
        list.Where(x => x.TaskId != completed)
            .Should()
            .HaveCount(2)
            .And.OnlyContain(x => x.Status == DailyTaskStatus.Active && x.SecondsLeft == 23 * 3600);
        (await grain.ClaimAsync(completed, Ct)).Should().BeTrue();
    }

    private IPlayerDailyTaskGrain Grain()
    {
        var grain = GrainHarness.Create(
            typeof(AchievementModule).Assembly,
            "Turbo.Achievements.Grains.PlayerDailyTaskGrain",
            _fakes,
            _db
        );
        RoomHarness.SetField(grain, "_time", _clock);

        return (IPlayerDailyTaskGrain)grain;
    }

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

    private T Single<T>()
        where T : IComposer => Sent<T>().Should().ContainSingle().Subject;

    private static DailyTaskDefinitionEntity Definition(
        int id,
        string code,
        string type,
        string target,
        int repeats,
        int duckets
    ) =>
        new()
        {
            Id = id,
            Code = code,
            QuestType = type,
            Target = target,
            RequiredRepeats = repeats,
            RewardAmount = duckets,
        };
}
