using System.Collections.Immutable;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementSyncTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private (AchievementCatalog Catalog, AchievementSync Sync) New(
        AchievementConfig? config = null,
        IReadOnlyDictionary<string, string>? texts = null
    )
    {
        var dictionary = texts ?? new Dictionary<string, string>();
        _fakes.Handlers[nameof(IHotelTextProvider.TryGetText)] = call =>
        {
            if (dictionary.TryGetValue((string)call.Args[0]!, out var value))
            {
                call.Args[1] = value;
                return true;
            }
            call.Args[1] = string.Empty;
            return false;
        };
        var options = Options.Create(config ?? new AchievementConfig());
        var texts2 = _fakes.Create<IHotelTextProvider>();
        var catalog = new AchievementCatalog(
            _db,
            _fakes.Create<ICurrencyTypeProvider>(),
            options,
            texts2
        );

        return (catalog, new AchievementSync(catalog, texts2, options));
    }

    private static AchievementDefinition Definition(
        int id,
        string key,
        string category = "identity",
        AchievementState state = AchievementState.Disabled
    ) =>
        new()
        {
            Id = id,
            Key = key,
            Revision = 1,
            Category = category,
            Source = AchievementSources.FIGURE,
            Reducer = AchievementReducer.Counter,
            State = state,
            Levels = [new() { Requirement = 1, BadgeCode = $"ACH_{key.Replace('-', '_')}1" }],
        };

    private static async Task<AchievementSyncReport> SyncAsync(
        AchievementSync sync,
        ImmutableArray<AchievementDefinition> file,
        bool apply = true,
        string operation = "sync-op"
    ) => await sync.SyncAsync(file, apply, "tests", "sync", operation, Ct);

    private async Task SeedAsync(
        AchievementCatalog catalog,
        params AchievementDefinition[] definitions
    ) => await catalog.ImportAsync([.. definitions], true, "tests", "seed", "seed-op", Ct);

    [Fact]
    public async Task NewKeysAreCreatedMatchingOnesLeftAloneEditedOnesRevisedAndMissingOnesReportedOnly()
    {
        var (catalog, sync) = New();
        await SeedAsync(
            catalog,
            Definition(100500, "a"),
            Definition(100501, "b"),
            Definition(100502, "c")
        );

        var report = await SyncAsync(
            sync,
            [
                Definition(100500, "a"),
                Definition(100501, "b") with
                {
                    Order = 9,
                },
                Definition(100503, "d"),
            ]
        );

        report.Created.Should().Equal("d");
        report.Revised.Should().Equal([("b", 2)]);
        report.Unchanged.Should().Equal("a");
        report.NotInFile.Should().Equal("c");
        report.HasProblems.Should().BeFalse();
        report.Applied.Should().BeTrue();
        catalog
            .Current.Select(x => (x.Key, x.Revision))
            .Should()
            .BeEquivalentTo([("a", 1), ("b", 2), ("c", 1), ("d", 1)]);
    }

    [Fact]
    public async Task ADryRunPublishesNothingAndSayingSoIsHonest()
    {
        var (catalog, sync) = New();
        await SeedAsync(catalog, Definition(100500, "a"));

        var report = await SyncAsync(
            sync,
            [Definition(100500, "a") with { Order = 3 }, Definition(100501, "b")],
            apply: false
        );

        report.Applied.Should().BeFalse();
        report.ChangesCatalog.Should().BeTrue();
        catalog.Current.Should().ContainSingle().Which.Revision.Should().Be(1);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(Ct)).Should().Be(1);
        report.ToLines(apply: false).Last().Should().Contain("Dry run");
    }

    [Fact]
    public async Task SyncingTheSameFileTwiceChangesNothingTheSecondTime()
    {
        var (catalog, sync) = New();
        var file = ImmutableArray.Create(
            Definition(100500, "a") with
            {
                Levels =
                [
                    new() { Requirement = 1, BadgeCode = "ACH_a1" },
                    new() { Requirement = 5, BadgeCode = "ACH_a2" },
                ],
            },
            Definition(100501, "b")
        );
        await SyncAsync(sync, file, operation: "first");

        // A real file is parsed afresh each time, so none of its arrays is the catalog's own.
        var reparsed = AchievementDefinitionJson.ReadAll(JsonSerializer.Serialize(file));

        var second = await SyncAsync(sync, reparsed, operation: "second");

        second.Unchanged.Should().BeEquivalentTo("a", "b");
        second.ChangesCatalog.Should().BeFalse();
        second.Applied.Should().BeFalse();
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task EveryProblemIsListedAtOnceAndNothingIsAppliedEvenForTheValidOnes()
    {
        var (catalog, sync) = New();
        var badKey = Definition(100500, "ok-key") with { Key = "Bad Key" };
        var badLevels = Definition(100501, "bad-levels") with
        {
            Levels =
            [
                new() { Requirement = 5, BadgeCode = "ACH_bad_levels1" },
                new() { Requirement = 2, BadgeCode = "ACH_bad_levels2" },
            ],
        };

        var report = await SyncAsync(sync, [badKey, badLevels, Definition(100502, "fine")]);

        report.HasProblems.Should().BeTrue();
        report.Problems.Should().HaveCount(2);
        report.Problems.Should().Contain(x => x.StartsWith("Bad Key"));
        report.Problems.Should().Contain(x => x.StartsWith("bad-levels"));
        report.Applied.Should().BeFalse();
        catalog.Current.Should().BeEmpty();
        report.ToLines(apply: true).Last().Should().Contain("Nothing applied");
    }

    [Fact]
    public async Task TheRevisionInTheFileIsIgnoredBecauseSyncNumbersThem()
    {
        var (catalog, sync) = New();
        await SeedAsync(catalog, Definition(100500, "a"));

        var report = await SyncAsync(
            sync,
            [Definition(100500, "a") with { Revision = 99, Order = 4 }]
        );

        report.Revised.Should().Equal([("a", 2)]);
        catalog.Current.Single().Revision.Should().Be(2);
    }

    [Fact]
    public async Task ARepeatedKeyAndAChangedIdAreProblems()
    {
        var (catalog, sync) = New();
        await SeedAsync(catalog, Definition(100500, "a"));

        var repeated = await SyncAsync(sync, [Definition(100501, "b"), Definition(100502, "B")]);
        var moved = await SyncAsync(sync, [Definition(100999, "a")]);

        repeated.Problems.Should().ContainSingle().Which.Should().Contain("2 times");
        moved.Problems.Should().ContainSingle().Which.Should().Contain("never changes");
        catalog.Current.Should().ContainSingle();
    }

    [Fact]
    public async Task TextsAndImagesNeededByAListedAchievementAreSpelledOutReadyToPaste()
    {
        var (catalog, sync) = New();

        var report = await SyncAsync(
            sync,
            [Definition(100500, "twelve-days", "christmas", AchievementState.OffSeason)]
        );

        report.MissingTexts.Should().Contain("quests.christmas.name=Christmas");
        report.MissingTexts.Should().Contain("badge_name_ACH_twelve_days=Twelve Days");
        report.MissingTexts.Should().Contain(x => x.StartsWith("badge_desc_ACH_twelve_days="));
        report.MissingBadgeImages.Should().Equal("ACH_twelve_days1.png");
        report.HasProblems.Should().BeFalse();
        report.Applied.Should().BeTrue();
    }

    [Fact]
    public async Task NothingIsMissingWhenTheTextsAndImagesAlreadyExist()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"achievement-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "ACH_twelve_days1.png"), []);
        var (_, sync) = New(
            new AchievementConfig { BadgeAssetDirectory = directory },
            new Dictionary<string, string>
            {
                ["quests.christmas.name"] = "Christmas",
                ["badge_name_ACH_twelve_days"] = "Twelve Days",
                ["badge_desc_ACH_twelve_days"] = "Log in on %limit% days.",
            }
        );

        var report = await SyncAsync(
            sync,
            [Definition(100500, "twelve-days", "christmas", AchievementState.Enabled)]
        );

        report.MissingTexts.Should().BeEmpty();
        report.MissingBadgeImages.Should().BeEmpty();
        report.HasProblems.Should().BeFalse();
        Directory.Delete(directory, recursive: true);
    }

    [Fact]
    public async Task AnEnabledAchievementWithoutItsAssetsIsAProblemAndTheReportSaysWhatToAdd()
    {
        var (catalog, sync) = New();

        var report = await SyncAsync(
            sync,
            [Definition(100500, "needs-things", "christmas", AchievementState.Enabled)]
        );

        report.HasProblems.Should().BeTrue();
        report.MissingBadgeImages.Should().Equal("ACH_needs_things1.png");
        report.MissingTexts.Should().NotBeEmpty();
        catalog.Current.Should().BeEmpty();
    }

    [Fact]
    public async Task ADisabledAchievementAsksForNoTextsOrImagesYet()
    {
        var (_, sync) = New();

        var report = await SyncAsync(sync, [Definition(100500, "later", "christmas")]);

        report.MissingTexts.Should().BeEmpty();
        report.MissingBadgeImages.Should().BeEmpty();
    }
}
