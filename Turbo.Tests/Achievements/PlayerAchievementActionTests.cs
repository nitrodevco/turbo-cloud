using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Achievements;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Players;
using Turbo.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class PlayerAchievementActionTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private const int PLAYER_ID = 44;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task FigureChangeRecordsOnlySuccessfulChangesAndNotNoOps()
    {
        _db.Insert(NewPlayer());
        var grain = NewGrain(currentFigure: "hd-180-1");

        await grain.SetFigureAsync("hd-180-1", AvatarGenderType.Male, Ct);
        await grain.SetFigureAsync("hd-200-1", AvatarGenderType.Male, Ct);
        await grain.SetFigureAsync("hd-200-1", AvatarGenderType.Male, Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementFacts.CountAsync(x => x.Source == AchievementSources.FIGURE, Ct))
            .Should()
            .Be(1);
        (await db.Players.SingleAsync(x => x.Id == PLAYER_ID, Ct)).Figure.Should().Be("hd-200-1");
    }

    [Fact]
    public async Task MottoChangeRecordsOnlySuccessfulChangesAndNotNoOps()
    {
        _db.Insert(NewPlayer());
        var grain = NewGrain(currentMotto: "existing");

        await grain.SetMottoAsync("existing", Ct);
        await grain.SetMottoAsync("new motto", Ct);
        await grain.SetMottoAsync("new motto", Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementFacts.CountAsync(x => x.Source == AchievementSources.MOTTO, Ct))
            .Should()
            .Be(1);
        (await db.Players.SingleAsync(x => x.Id == PLAYER_ID, Ct)).Motto.Should().Be("new motto");
    }

    [Fact]
    public async Task FailedProfileWriteRollsBackMutationAndDoesNotRecordActionFact()
    {
        var grain = NewGrain(currentMotto: "before-write");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            grain.SetMottoAsync("failed-write", Ct)
        );

        var state = RoomHarness.GetMember(grain, "_state")!;
        Assert.Equal("before-write", state.GetType().GetProperty("Motto")!.GetValue(state));
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementFacts.CountAsync(Ct)).Should().Be(0);
    }

    private IPlayerGrain NewGrain(string currentFigure = "hd-180-1", string currentMotto = "")
    {
        var grain = GrainHarness.Create(
            typeof(PlayerModule).Assembly,
            "Turbo.Players.Grains.PlayerGrain",
            _fakes,
            _db,
            PLAYER_ID
        );
        var state = RoomHarness.GetMember(grain, "_state")!;
        RoomHarness.SetMember(state, "Name", "player-action-test");
        RoomHarness.SetMember(state, "Figure", currentFigure);
        RoomHarness.SetMember(state, "Gender", AvatarGenderType.Male);
        RoomHarness.SetMember(state, "Motto", currentMotto);
        // With no figure data taken in, a player wears what they ask for.
        _fakes.Handlers["FitAsync"] = call => Task.FromResult((string)call.Args[1]!);
        RoomHarness.SetField(
            grain,
            "_figurePolicy",
            _fakes.Create<Turbo.Primitives.Figures.IPlayerFigurePolicy>()
        );
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
            Name = "player-action-test",
            Figure = "hd-180-1",
            Gender = AvatarGenderType.Male,
            PlayerStatus = PlayerStatusType.Offline,
            Motto = "existing",
        };
}
