using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Achievements;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Messages.Outgoing.Avatar;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Events;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

public sealed class PlayerLookOverrideGrainTests : IDisposable
{
    private const int PLAYER_ID = 51;
    private const string SAVED = "hd-180-1.hr-100-61.ch-210-66.lg-270-82";
    private const string UNIFORM = "ch-3030-110.lg-3006-110";

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TestEventBus _events = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PlayerLookOverrideGrainTests()
    {
        _events.Record<PlayerLookOverrideChangedEvent>();
        _db.Insert(NewPlayer());
        SessionIsActive(true);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a figure")]
    [InlineData("hd-180-x")]
    [InlineData("HD-180-1")]
    public async Task InvalidFigureIsRejectedAndNothingIsShown(string figure)
    {
        var grain = NewGrain();

        var set = await grain.SetLookOverrideAsync(figure, null, LookOverrideMode.Replace, Ct);

        set.Should().BeFalse();
        (await grain.GetLookOverrideAsync(Ct)).Should().BeNull();
        (await grain.GetSummaryAsync(Ct)).Figure.Should().Be(SAVED);
        _fakes.Log.Of("OnPlayerUpdatedAsync").Should().BeEmpty();
        _events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownModeIsRejected()
    {
        var grain = NewGrain();

        (await grain.SetLookOverrideAsync(UNIFORM, null, (LookOverrideMode)42, Ct))
            .Should()
            .BeFalse();

        (await grain.GetLookOverrideAsync(Ct)).Should().BeNull();
    }

    [Fact]
    public async Task OverrideIsRefusedWithoutASession()
    {
        SessionIsActive(false);
        var grain = NewGrain();

        var set = await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.Replace, Ct);

        set.Should().BeFalse();
        (await grain.GetLookOverrideAsync(Ct)).Should().BeNull();
    }

    [Fact]
    public async Task ClearingWhenNoneIsSetReportsFalseAndShowsNothing()
    {
        var grain = NewGrain();

        (await grain.ClearLookOverrideAsync(Ct)).Should().BeFalse();

        _fakes.Log.Of("OnPlayerUpdatedAsync").Should().BeEmpty();
        _fakes.Log.Of("SendComposerAsync").Should().BeEmpty();
        _events.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplaceOverrideIsWhatTheRoomAndTheOwnClientAreShown()
    {
        var grain = NewGrain();

        var set = await grain.SetLookOverrideAsync(
            UNIFORM,
            AvatarGenderType.Female,
            LookOverrideMode.Replace,
            Ct
        );

        set.Should().BeTrue();
        var summary = await grain.GetSummaryAsync(Ct);
        summary.Figure.Should().Be(UNIFORM);
        summary.Gender.Should().Be(AvatarGenderType.Female);
        LastPresenceUpdate().Figure.Should().Be(UNIFORM);
        LastOwnFigureUpdate().Figure.Should().Be(UNIFORM);
        LastOwnFigureUpdate().Gender.Should().Be(AvatarGenderType.Female);
    }

    [Fact]
    public async Task MergedOverrideLayersOverTheSavedFigure()
    {
        var grain = NewGrain();

        await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.MergeParts, Ct);

        (await grain.GetSummaryAsync(Ct))
            .Figure.Should()
            .Be("hd-180-1.hr-100-61.ch-3030-110.lg-3006-110");
        LastPresenceUpdate().Figure.Should().Be("hd-180-1.hr-100-61.ch-3030-110.lg-3006-110");
    }

    [Fact]
    public async Task OverrideIsNotWrittenToTheDatabase()
    {
        var grain = NewGrain();

        await grain.SetLookOverrideAsync(
            UNIFORM,
            AvatarGenderType.Female,
            LookOverrideMode.Replace,
            Ct
        );
        // Any later save of the profile writes the row from the grain's state.
        await grain.SetMottoAsync("still saving", Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        var row = await db.Players.SingleAsync(x => x.Id == PLAYER_ID, Ct);
        row.Figure.Should().Be(SAVED);
        row.Gender.Should().Be(AvatarGenderType.Male);
        row.Motto.Should().Be("still saving");
    }

    [Fact]
    public async Task ClearingBringsTheSavedLookBackToEveryone()
    {
        var grain = NewGrain();
        await grain.SetLookOverrideAsync(
            UNIFORM,
            AvatarGenderType.Female,
            LookOverrideMode.Replace,
            Ct
        );

        (await grain.ClearLookOverrideAsync(Ct)).Should().BeTrue();

        var summary = await grain.GetSummaryAsync(Ct);
        summary.Figure.Should().Be(SAVED);
        summary.Gender.Should().Be(AvatarGenderType.Male);
        LastPresenceUpdate().Figure.Should().Be(SAVED);
        LastOwnFigureUpdate().Figure.Should().Be(SAVED);
    }

    [Fact]
    public async Task DisconnectClearsTheOverrideAndTellsTheRoom()
    {
        var grain = NewGrain();
        await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.Replace, Ct);

        await grain.SetOnlineStatusAsync(false, Ct);

        (await grain.GetLookOverrideAsync(Ct)).Should().BeNull();
        (await grain.GetSummaryAsync(Ct)).Figure.Should().Be(SAVED);
        LastPresenceUpdate().Figure.Should().Be(SAVED);
        await EventsPublishedAsync(2);
        _events.Of<PlayerLookOverrideChangedEvent>().Last().Current.Should().BeNull();
    }

    [Fact]
    public async Task OwnFigureChangeWhileOverriddenSavesButTheOverrideStaysShown()
    {
        var grain = NewGrain();
        await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.Replace, Ct);

        await grain.SetFigureAsync("hd-200-2.hr-5-5", AvatarGenderType.Female, Ct);

        var summary = await grain.GetSummaryAsync(Ct);
        summary.Figure.Should().Be(UNIFORM);
        LastOwnFigureUpdate().Figure.Should().Be(UNIFORM);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.Players.SingleAsync(x => x.Id == PLAYER_ID, Ct))
            .Figure.Should()
            .Be("hd-200-2.hr-5-5");

        await grain.ClearLookOverrideAsync(Ct);

        (await grain.GetSummaryAsync(Ct)).Figure.Should().Be("hd-200-2.hr-5-5");
        (await grain.GetSummaryAsync(Ct)).Gender.Should().Be(AvatarGenderType.Female);
    }

    [Fact]
    public async Task MergedOverrideFollowsALaterSavedFigureChange()
    {
        var grain = NewGrain();
        await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.MergeParts, Ct);

        await grain.SetFigureAsync("hd-200-2.hr-5-5.ch-1-1", AvatarGenderType.Male, Ct);

        (await grain.GetSummaryAsync(Ct))
            .Figure.Should()
            .Be("hd-200-2.hr-5-5.ch-3030-110.lg-3006-110");
    }

    [Fact]
    public async Task SettingTheSameOverrideAgainShowsNothingNew()
    {
        var grain = NewGrain();
        await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.Replace, Ct);
        var updates = _fakes.Log.Of("OnPlayerUpdatedAsync").Count();

        (await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.Replace, Ct))
            .Should()
            .BeTrue();

        _fakes.Log.Of("OnPlayerUpdatedAsync").Should().HaveCount(updates);
    }

    [Fact]
    public async Task ChangeEventCarriesPreviousCurrentAndTheLookShown()
    {
        var grain = NewGrain();
        await grain.SetLookOverrideAsync(UNIFORM, null, LookOverrideMode.Replace, Ct);
        await grain.SetLookOverrideAsync("ch-1-1", null, LookOverrideMode.MergeParts, Ct);

        await EventsPublishedAsync(2);

        var second = _events
            .Of<PlayerLookOverrideChangedEvent>()
            .Single(e => e.Previous is not null);
        second.PlayerId.Value.Should().Be(PLAYER_ID);
        second.Previous!.Figure.Should().Be(UNIFORM);
        second.Current!.Figure.Should().Be("ch-1-1");
        second.Figure.Should().Be("hd-180-1.hr-100-61.ch-1-1.lg-270-82");
    }

    private void SessionIsActive(bool active) =>
        _fakes.Handlers["HasActiveSessionAsync"] = _ => Task.FromResult(active);

    private PlayerSummarySnapshot LastPresenceUpdate() =>
        (PlayerSummarySnapshot)_fakes.Log.Of("OnPlayerUpdatedAsync").Last().Args[0]!;

    private FigureUpdateEventMessageComposer LastOwnFigureUpdate() =>
        _fakes
            .Log.Of("SendComposerAsync")
            .Select(c => c.Args[0])
            .OfType<FigureUpdateEventMessageComposer>()
            .Last();

    // Events are published without being awaited, so give the pipeline a moment to deliver.
    private async Task EventsPublishedAsync(int count)
    {
        for (var i = 0; i < 100 && _events.Published.Count < count; i++)
            await Task.Delay(20, Ct);

        _events.Published.Should().HaveCount(count);
    }

    private IPlayerGrain NewGrain()
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerGrain",
            _fakes,
            _db,
            PLAYER_ID
        );
        var state = RoomHarness.GetMember(grain, "_state")!;
        RoomHarness.SetMember(state, "Name", "look-override-test");
        RoomHarness.SetMember(state, "Figure", SAVED);
        RoomHarness.SetMember(state, "Gender", AvatarGenderType.Male);
        RoomHarness.SetField(grain, "_eventSystem", _events.System);
        RoomHarness.SetField(
            grain,
            "_achievementFacts",
            new AchievementFactRecorder(new ListeningAchievementCatalog())
        );
        return (IPlayerGrain)grain;
    }

    private static PlayerEntity NewPlayer() =>
        new()
        {
            Id = PLAYER_ID,
            Name = "look-override-test",
            Figure = SAVED,
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            Motto = "",
        };
}
