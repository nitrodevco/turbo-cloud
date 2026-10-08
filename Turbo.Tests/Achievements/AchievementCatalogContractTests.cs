using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Database.Achievements;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementCatalogContractTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task InvalidImportIsAtomicAndInvalidReloadKeepsPublishedCatalog()
    {
        var catalog = NewCatalog();
        var published = Definition(100100, "catalog-published", AchievementSources.FIGURE);
        await catalog.ImportAsync([published], true, "tests", "initial", "catalog-op-1", Ct);

        var invalid = Definition(100101, published.Key, AchievementSources.FIGURE);
        var import = () =>
            catalog.ImportAsync([invalid], true, "tests", "duplicate key", "catalog-op-2", Ct);
        await import.Should().ThrowAsync<InvalidOperationException>();
        catalog.Current.Should().ContainSingle().Which.Should().BeEquivalentTo(published);
        await using (var db = await _db.CreateDbContextAsync(Ct))
        {
            (await db.AchievementDefinitions.CountAsync(Ct)).Should().Be(1);
            (await db.AchievementAudit.CountAsync(Ct)).Should().Be(1);
        }

        _db.Insert(
            new AchievementDefinitionEntity
            {
                AchievementId = 100102,
                Revision = 1,
                DefinitionJson = "{",
            }
        );
        var reload = () => catalog.ReloadAsync(Ct);
        await reload.Should().ThrowAsync<JsonException>();
        catalog.Current.Should().ContainSingle().Which.Should().BeEquivalentTo(published);
    }

    [Fact]
    public async Task UnloadedSourceDoesNotRewriteFactsFrozenAgainstItsDefinition()
    {
        var assetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-source-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(assetDirectory);
        const string badgeCode = "ACH_Ephemeral1";
        File.WriteAllBytes(Path.Combine(assetDirectory, badgeCode + ".png"), []);
        var catalog = NewCatalog(
            new AchievementConfig { BadgeAssetDirectory = assetDirectory },
            new Dictionary<string, string>
            {
                ["badge_name_" + badgeCode] = "Ephemeral badge",
                ["badge_desc_" + badgeCode] = "Ephemeral badge description",
            }
        );
        var source = new AchievementSourceDefinition(
            "test.ephemeral",
            1,
            AchievementReducer.Counter
        );
        using (catalog.RegisterSources([source]))
        {
            var definition = Definition(100110, "ephemeral-source", source.Key);
            definition = definition with
            {
                State = AchievementState.Enabled,
                Levels = [new() { Requirement = 1, BadgeCode = badgeCode }],
            };
            await catalog.ImportAsync(
                [definition],
                true,
                "tests",
                "source active",
                "catalog-source-op",
                Ct
            );

            await using var db = await _db.CreateDbContextAsync(Ct);
            new AchievementFactRecorder(catalog).Record(
                db,
                (PlayerId)1,
                new AchievementFact
                {
                    OperationId = "ephemeral-fact-1",
                    Source = source.Key,
                    OccurredAtUtc = DateTime.UtcNow,
                }
            );
            await db.SaveChangesAsync(Ct);
        }

        await catalog.ReloadAsync(Ct);
        catalog.Current.Should().ContainSingle(x => x.Id == 100110);
        await using var check = await _db.CreateDbContextAsync(Ct);
        var fact = await check.AchievementFacts.SingleAsync(Ct);
        using var bindings = JsonDocument.Parse(fact.BindingsJson);
        bindings.RootElement.GetArrayLength().Should().Be(1);
        bindings.RootElement[0].GetProperty("Id").GetInt32().Should().Be(100110);
        Directory.Delete(assetDirectory, recursive: true);
    }

    [Fact]
    public async Task EnabledDefinitionsAcceptExactAndNumericBaseBadgeLocalizationKeys()
    {
        var assetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-badges-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(assetDirectory);
        try
        {
            File.WriteAllBytes(Path.Combine(assetDirectory, "ACH_RespectGiven1.png"), []);
            File.WriteAllBytes(Path.Combine(assetDirectory, "ACH_RegistrationDuration1.png"), []);
            var texts = new Dictionary<string, string>
            {
                ["badge_name_ACH_RespectGiven1"] = "Exact name",
                ["badge_desc_ACH_RespectGiven1"] = "Exact description",
                ["badge_name_ACH_RegistrationDuration"] = "Base name",
                ["badge_desc_ACH_RegistrationDuration"] = "Base description",
            };
            var catalog = NewCatalog(
                new AchievementConfig { BadgeAssetDirectory = assetDirectory },
                texts
            );
            var exact = Definition(100120, "respect-text", AchievementSources.RESPECT_GIVEN) with
            {
                State = AchievementState.Enabled,
                Levels = [new() { Requirement = 1, BadgeCode = "ACH_RespectGiven1" }],
            };
            var numericBase = Definition(
                100121,
                "registration-text",
                AchievementSources.ACCOUNT_AGE
            ) with
            {
                State = AchievementState.Enabled,
                Reducer = AchievementReducer.Maximum,
                Levels = [new() { Requirement = 1, BadgeCode = "ACH_RegistrationDuration1" }],
            };

            await catalog.ImportAsync(
                [exact, numericBase],
                true,
                "tests",
                "localization",
                "catalog-text-op",
                Ct
            );

            catalog
                .Current.SelectMany(x => x.Levels)
                .Select(x => x.BadgeCode)
                .Should()
                .ContainInOrder("ACH_RespectGiven1", "ACH_RegistrationDuration1");
        }
        finally
        {
            Directory.Delete(assetDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task NewRevisionMayChangeDisplayConversionButCannotChangeStoredSourceMeaning()
    {
        var catalog = NewCatalog();
        var initial = Definition(100130, "display-units", AchievementSources.HC) with
        {
            Reducer = AchievementReducer.Maximum,
            UnitDivisor = 86400,
        };
        await catalog.ImportAsync([initial], true, "tests", "initial", "units-initial", Ct);
        var revised = initial with
        {
            Revision = 2,
            UnitDivisor = 31 * 86400,
            Levels = [new() { Requirement = 0, BadgeCode = "ACH_display_units1" }],
        };
        await catalog.ImportAsync(
            [revised],
            true,
            "tests",
            "display conversion",
            "units-revised",
            Ct
        );
        catalog.Current.Single().UnitDivisor.Should().Be(31 * 86400);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementDefinitions.CountAsync(Ct)).Should().Be(2);
        var changedSource = revised with { Revision = 3, Source = AchievementSources.PETS };
        var import = () =>
            catalog.ImportAsync(
                [changedSource],
                true,
                "tests",
                "invalid source",
                "units-invalid",
                Ct
            );
        await import.Should().ThrowAsync<InvalidOperationException>();
        catalog.Current.Single().Revision.Should().Be(2);
    }

    [Fact]
    public async Task WithNoPackRegisteredAnEmptyHotelStaysEmpty()
    {
        var existing = NewCatalog(new AchievementConfig { InstallDefaults = false });

        await existing.ReloadAsync(Ct);

        existing.Current.Should().BeEmpty();
    }

    [Fact]
    public async Task AHabboPackThatCannotInstallLeavesTheHotelsOwnCatalogAlone()
    {
        var custom = Definition(100100, "hotel-owned", AchievementSources.FIGURE);
        _db.Insert(
            new AchievementDefinitionEntity
            {
                AchievementId = custom.Id,
                Revision = custom.Revision,
                DefinitionJson = JsonSerializer.Serialize(custom),
            }
        );
        var catalog = NewCatalog(packs: new AchievementPackRegistry([new HabboAchievementPack()]));

        await catalog.ReloadAsync(Ct);

        catalog.Current.Should().ContainSingle().Which.Id.Should().Be(100100);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(Ct)).Should().Be(0);
    }

    [Fact]
    public async Task AHotelMissingBadgeAssetsKeepsAnEmptyCatalogWithoutFailingStartup()
    {
        var catalog = NewCatalog();

        await catalog.ReloadAsync(Ct);

        catalog.Current.Should().BeEmpty();
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementDefinitions.CountAsync(Ct)).Should().Be(0);
    }

    [Theory]
    [InlineData(AchievementState.WiredControlled)]
    [InlineData((AchievementState)9)]
    public async Task UnsupportedStatesAreRejected(AchievementState state)
    {
        var catalog = NewCatalog();
        var definition = Definition(100130, "unsupported-state", AchievementSources.FIGURE) with
        {
            State = state,
        };

        var import = () =>
            catalog.ImportAsync([definition], false, "tests", "validate", "state-op-1", Ct);

        await import.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(AchievementState.Disabled)]
    [InlineData(AchievementState.Archived)]
    [InlineData(AchievementState.OffSeason)]
    public async Task OnlyEnabledAchievementsNeedBadgeAssetsAndTexts(AchievementState state)
    {
        var catalog = NewCatalog();
        var definition = Definition(100131, "no-assets-needed", AchievementSources.FIGURE) with
        {
            State = state,
        };

        await catalog.ImportAsync([definition], true, "tests", "retire", "state-op-2", Ct);

        catalog.Current.Should().ContainSingle().Which.State.Should().Be(state);
        var enabled = definition with { Revision = 2, State = AchievementState.Enabled };
        var import = () =>
            catalog.ImportAsync([enabled], false, "tests", "enable", "state-op-3", Ct);
        await import.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AnActiveWindowNeedsUtcTimesWithTheStartBeforeTheEnd()
    {
        var catalog = NewCatalog();
        var start = new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);
        var definition = Definition(100140, "windowed", AchievementSources.FIGURE);

        foreach (
            var invalid in new[]
            {
                definition with
                {
                    ActiveFromUtc = start,
                    ActiveUntilUtc = start,
                },
                definition with
                {
                    ActiveFromUtc = start,
                    ActiveUntilUtc = start.AddDays(-1),
                },
                definition with
                {
                    ActiveFromUtc = DateTime.SpecifyKind(start, DateTimeKind.Local),
                },
                definition with
                {
                    ActiveUntilUtc = DateTime.SpecifyKind(start, DateTimeKind.Unspecified),
                },
            }
        )
        {
            var import = () =>
                catalog.ImportAsync([invalid], false, "tests", "validate", "window-op-1", Ct);
            await import.Should().ThrowAsync<InvalidOperationException>();
        }

        var valid = definition with { ActiveFromUtc = start, ActiveUntilUtc = start.AddDays(12) };
        await catalog.ImportAsync([valid], true, "tests", "window", "window-op-2", Ct);
        catalog
            .Current.Should()
            .ContainSingle()
            .Which.ActiveUntilUtc.Should()
            .Be(valid.ActiveUntilUtc);
    }

    [Theory]
    [InlineData(AchievementSources.LOGIN, AchievementReducer.CalendarStreak, true)]
    [InlineData(AchievementSources.LOGIN, AchievementReducer.Counter, true)]
    [InlineData(AchievementSources.LOGIN, AchievementReducer.Distinct, true)]
    [InlineData(AchievementSources.LOGIN, AchievementReducer.ElapsedSeconds, false)]
    [InlineData(AchievementSources.VISIT, AchievementReducer.Counter, true)]
    [InlineData(AchievementSources.ONLINE, AchievementReducer.Counter, false)]
    [InlineData(AchievementSources.FIGURE, AchievementReducer.Maximum, false)]
    public async Task ADefinitionMayUseAnyReducerItsSourceAllows(
        string source,
        AchievementReducer reducer,
        bool accepted
    )
    {
        var catalog = NewCatalog();
        var definition = Definition(100150, "reducer-choice", source) with
        {
            Reducer = reducer,
            Match =
                reducer == AchievementReducer.Distinct && source == AchievementSources.LOGIN
                    ? new AchievementMatch { ValueFrom = AchievementValueSource.UtcDate }
                    : null,
        };

        var import = () =>
            catalog.ImportAsync([definition], false, "tests", "validate", "reducer-op", Ct);

        if (accepted)
            await import.Should().NotThrowAsync();
        else
            await import.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AMatchMustMakeSense()
    {
        var catalog = NewCatalog();
        var distinct = Definition(100151, "match-rules", AchievementSources.LOGIN) with
        {
            Reducer = AchievementReducer.Distinct,
        };
        var utcDate = new AchievementMatch { ValueFrom = AchievementValueSource.UtcDate };

        foreach (
            var invalid in new[]
            {
                distinct with
                {
                    Reducer = AchievementReducer.Counter,
                    Match = utcDate,
                },
                distinct with
                {
                    Match = utcDate with { Values = ["2026-12-25"] },
                },
                distinct with
                {
                    Match = new AchievementMatch { Values = [""] },
                },
                distinct with
                {
                    Match = new AchievementMatch { Values = [new string('x', 513)] },
                },
                distinct with
                {
                    Match = new AchievementMatch { ValueFrom = (AchievementValueSource)7 },
                },
                distinct with
                {
                    Match = new AchievementMatch
                    {
                        Values = [.. Enumerable.Range(0, 1001).Select(x => x.ToString())],
                    },
                },
            }
        )
        {
            var import = () =>
                catalog.ImportAsync([invalid], false, "tests", "validate", "match-op-1", Ct);
            await import.Should().ThrowAsync<InvalidOperationException>();
        }

        await catalog.ImportAsync(
            [distinct with { Match = utcDate }],
            false,
            "tests",
            "validate",
            "match-op-2",
            Ct
        );
    }

    [Fact]
    public void ASourceCannotListAnUndefinedExtraReducer()
    {
        var catalog = NewCatalog();

        var register = () =>
            catalog.RegisterSources([
                new AchievementSourceDefinition(
                    "plugin.bad-reducer",
                    1,
                    AchievementReducer.Counter,
                    [(AchievementReducer)99]
                ),
            ]);

        register.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task APackAddsOnlyWhatTheHotelDoesNotHave()
    {
        var owned = Definition(2001, "alpha", AchievementSources.FIGURE) with { Revision = 5 };
        _db.Insert(
            new AchievementDefinitionEntity
            {
                AchievementId = owned.Id,
                Revision = owned.Revision,
                DefinitionJson = JsonSerializer.Serialize(owned),
            }
        );
        var packs = new AchievementPackRegistry([SeasonalPack(1)]);
        var catalog = NewCatalog(packs: packs);

        await catalog.ReloadAsync(Ct);

        catalog.Current.Select(x => (x.Key, x.Revision)).Should().Equal(("alpha", 5), ("beta", 1));
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.SingleAsync(Ct))
            .OperationId.Should()
            .MatchRegex("^pack:seasonal:1:[0-9A-F]{8}$");
    }

    [Fact]
    public async Task AnOwnersEditSurvivesANewVersionOfThePackAndItsNewAchievementsStillArrive()
    {
        var registry = new AchievementPackRegistry([]);
        var version1 = registry.Register(SeasonalPack(1));
        var catalog = NewCatalog(packs: registry);
        await catalog.ReloadAsync(Ct);
        var edited = catalog.Current.Single(x => x.Key == "alpha") with
        {
            Revision = 2,
            Levels =
            [
                new()
                {
                    Requirement = 7,
                    BadgeCode = "ACH_alpha1",
                    Score = 99,
                },
            ],
        };
        await catalog.ImportAsync([edited], true, "owner", "retune", "owner-edit", Ct);
        version1.Dispose();
        registry.Register(SeasonalPack(2, extra: true));

        await catalog.ReloadAsync(Ct);

        var alpha = catalog.Current.Single(x => x.Key == "alpha");
        alpha.Revision.Should().Be(2);
        alpha.Levels.Single().Requirement.Should().Be(7);
        catalog.Current.Select(x => x.Key).Should().BeEquivalentTo("alpha", "beta", "gamma");
    }

    [Fact]
    public async Task ReloadingAnAlreadyInstalledPackChangesNothing()
    {
        var catalog = NewCatalog(packs: new AchievementPackRegistry([SeasonalPack(1)]));

        await catalog.ReloadAsync(Ct);
        await catalog.ReloadAsync(Ct);
        await catalog.ReloadAsync(Ct);

        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(Ct)).Should().Be(1);
        (await db.AchievementDefinitions.CountAsync(Ct)).Should().Be(2);
    }

    [Fact]
    public async Task AnIdInAPacksRangeIsAcceptedOnlyWhilethePackIsRegisteredAndStoredRowsAlwaysLoad()
    {
        var inRange = Definition(2005, "in-range", AchievementSources.FIGURE);
        var without = NewCatalog();
        var import = () =>
            without.ImportAsync([inRange], false, "tests", "no pack", "range-op-1", Ct);
        await import.Should().ThrowAsync<InvalidOperationException>();

        var registry = new AchievementPackRegistry([]);
        var registration = registry.Register(SeasonalPack(1));
        var catalog = NewCatalog(packs: registry);
        await catalog.ImportAsync([inRange], true, "tests", "pack", "range-op-2", Ct);
        registration.Dispose();
        var reloaded = NewCatalog();

        await reloaded.ReloadAsync(Ct);

        reloaded.Current.Should().Contain(x => x.Key == "in-range");
    }

    [Fact]
    public async Task APackNeverTakesAnIdAnotherAchievementAlreadyUses()
    {
        var other = Definition(2002, "gamma", AchievementSources.FIGURE);
        _db.Insert(
            new AchievementDefinitionEntity
            {
                AchievementId = other.Id,
                Revision = other.Revision,
                DefinitionJson = JsonSerializer.Serialize(other),
            }
        );
        var catalog = NewCatalog(packs: new AchievementPackRegistry([SeasonalPack(1)]));

        await catalog.ReloadAsync(Ct);

        catalog.Current.Select(x => x.Key).Should().BeEquivalentTo("alpha", "gamma");
        catalog.Current.Single(x => x.Id == 2002).Key.Should().Be("gamma");
    }

    [Fact]
    public async Task APackThatCannotInstallNeitherStopsAnotherPackNorStartup()
    {
        var failing = new TestAchievementPack(
            "broken",
            1,
            [new(3000, 3010)],
            [
                Definition(3001, "needs-assets", AchievementSources.FIGURE) with
                {
                    State = AchievementState.Enabled,
                },
            ]
        );
        var catalog = NewCatalog(packs: new AchievementPackRegistry([failing, SeasonalPack(1)]));

        await catalog.ReloadAsync(Ct);

        catalog.Current.Select(x => x.Key).Should().BeEquivalentTo("alpha", "beta");
    }

    [Fact]
    public async Task TheHabboPackInstallsIntoAnEmptyHotelOnceThroughTheAuditedImport()
    {
        var assetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-pack-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(assetDirectory);
        var texts = new Dictionary<string, string>();
        foreach (
            var code in new HabboAchievementPack()
                .Definitions.Where(x => x.State == AchievementState.Enabled)
                .SelectMany(x => x.Levels)
                .Select(x => x.BadgeCode)
        )
        {
            File.WriteAllBytes(Path.Combine(assetDirectory, code + ".png"), []);
            texts["badge_name_" + code] = code;
            texts["badge_desc_" + code] = code;
        }
        var pack = new HabboAchievementPack();
        var catalog = NewCatalog(
            new AchievementConfig { BadgeAssetDirectory = assetDirectory },
            texts,
            new AchievementPackRegistry([pack])
        );

        await catalog.ReloadAsync(Ct);
        await catalog.ReloadAsync(Ct);

        catalog.Current.Should().HaveCount(pack.Definitions.Length);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.SingleAsync(Ct)).OperationId.Should().StartWith("pack:habbo:3:");
        Directory.Delete(assetDirectory, recursive: true);
    }

    [Fact]
    public async Task AHotelOnTheOlderPackHasItsUntouchedCrackablesRecordsHookedAndItsEditedOnesKept()
    {
        var assetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-pack-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(assetDirectory);
        var texts = new Dictionary<string, string>();
        foreach (
            var code in new HabboAchievementPack()
                .Definitions.SelectMany(x => x.Levels)
                .Select(x => x.BadgeCode)
        )
        {
            File.WriteAllBytes(Path.Combine(assetDirectory, code + ".png"), []);
            texts["badge_name_" + code] = code;
            texts["badge_desc_" + code] = code;
        }
        var current = new HabboAchievementPack();
        // Version 2 shipped the crackables records disabled on the placeholder source.
        var older = new TestAchievementPack(
            HabboAchievementPack.KEY,
            2,
            [.. current.IdRanges],
            [
                .. current.Definitions.Select(x =>
                    x.Source
                        is AchievementSources.CRACKABLE_HIT
                            or AchievementSources.CRACKABLE_CRACKED
                        ? x with
                        {
                            Source = AchievementSources.UNHOOKED,
                            Match = null,
                            State = AchievementState.Disabled,
                        }
                        : x
                ),
            ]
        );
        var config = new AchievementConfig { BadgeAssetDirectory = assetDirectory };
        var onOlder = NewCatalog(config, texts, new AchievementPackRegistry([older]));
        await onOlder.ReloadAsync(Ct);
        // The hotel edited one of them since: it is the hotel's now.
        var farmer = onOlder.Current.Single(x => x.Key == "farmer");
        await onOlder.ImportAsync(
            [farmer with { Revision = 2, Order = farmer.Order + 1 }],
            true,
            "admin",
            "reorder",
            "edit-farmer",
            Ct
        );

        var onCurrent = NewCatalog(config, texts, new AchievementPackRegistry([current]));
        await onCurrent.ReloadAsync(Ct);
        await onCurrent.ReloadAsync(Ct);

        var whacker = onCurrent.Current.Single(x => x.Key == "pinata-whacker");
        whacker.Source.Should().Be(AchievementSources.CRACKABLE_HIT);
        whacker.Match!.Values.Should().Equal("pinatawhacker");
        whacker.State.Should().Be(AchievementState.Enabled);
        whacker.Revision.Should().Be(2);
        onCurrent
            .Current.Single(x => x.Key == "pinata-breaker")
            .Source.Should()
            .Be(AchievementSources.CRACKABLE_CRACKED);
        var kept = onCurrent.Current.Single(x => x.Key == "farmer");
        kept.Source.Should().Be(AchievementSources.UNHOOKED);
        kept.Revision.Should().Be(2);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(x => x.OperationId.StartsWith("pack-hook:"), Ct))
            .Should()
            .Be(1);
        Directory.Delete(assetDirectory, recursive: true);
    }

    private static TestAchievementPack SeasonalPack(int version, bool extra = false) =>
        new(
            "seasonal",
            version,
            [new(2000, 2010)],
            [
                Definition(2001, "alpha", AchievementSources.FIGURE),
                Definition(2002, "beta", AchievementSources.FIGURE),
                .. extra
                    ? [Definition(2003, "gamma", AchievementSources.FIGURE)]
                    : Array.Empty<AchievementDefinition>(),
            ]
        );

    [Fact]
    public async Task RetiringPublishesANewRevisionThatKeepsEverythingElseAndIsAudited()
    {
        var catalog = NewCatalog();
        var start = new DateTime(2026, 12, 25, 0, 0, 0, DateTimeKind.Utc);
        var original = Definition(100160, "retire-me", AchievementSources.FIGURE) with
        {
            State = AchievementState.OffSeason,
            ActiveFromUtc = start,
            ActiveUntilUtc = start.AddDays(12),
            Order = 7,
        };
        await catalog.ImportAsync([original], true, "tests", "create", "retire-op-1", Ct);

        var change = await catalog.SetStateAsync(
            "RETIRE-ME",
            AchievementState.Archived,
            true,
            "console",
            "no longer awarded",
            "retire-op-2",
            Ct
        );

        change
            .Should()
            .Be(
                new AchievementStateChange(
                    "retire-me",
                    AchievementState.OffSeason,
                    AchievementState.Archived,
                    2,
                    true
                )
            );
        catalog
            .Current.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(original with { Revision = 2, State = AchievementState.Archived });
        await using var db = await _db.CreateDbContextAsync(Ct);
        var audit = await db.AchievementAudit.SingleAsync(x => x.OperationId == "retire-op-2", Ct);
        audit.Actor.Should().Be("console");
        audit.Reason.Should().Be("no longer awarded");
        (await db.AchievementDefinitions.CountAsync(x => x.AchievementId == 100160, Ct))
            .Should()
            .Be(2);
    }

    [Fact]
    public async Task ADryRunPublishesNothing()
    {
        var catalog = NewCatalog();
        var original = Definition(100161, "dry-run", AchievementSources.FIGURE);
        await catalog.ImportAsync([original], true, "tests", "create", "dry-op-1", Ct);

        var change = await catalog.SetStateAsync(
            "dry-run",
            AchievementState.Archived,
            false,
            "console",
            "preview",
            "dry-op-2",
            Ct
        );

        change.Changed.Should().BeTrue();
        change.Revision.Should().Be(2);
        catalog.Current.Single().Should().BeEquivalentTo(original);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(Ct)).Should().Be(1);
        (await db.AchievementDefinitions.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task AnUnknownKeyIsRefused()
    {
        var catalog = NewCatalog();

        var change = () =>
            catalog.SetStateAsync(
                "nothing-here",
                AchievementState.Archived,
                true,
                "console",
                "x",
                "none-op",
                Ct
            );

        await change.Should().ThrowAsync<InvalidOperationException>().WithMessage("*nothing-here*");
    }

    [Fact]
    public async Task AnAchievementAlreadyInTheStateIsLeftAlone()
    {
        var catalog = NewCatalog();
        var original = Definition(100162, "already", AchievementSources.FIGURE) with
        {
            State = AchievementState.Archived,
        };
        await catalog.ImportAsync([original], true, "tests", "create", "same-op-1", Ct);

        var change = await catalog.SetStateAsync(
            "already",
            AchievementState.Archived,
            true,
            "console",
            "again",
            "same-op-2",
            Ct
        );

        change.Changed.Should().BeFalse();
        change.Revision.Should().Be(1);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementAudit.CountAsync(Ct)).Should().Be(1);
    }

    [Fact]
    public async Task EachChangeOfStateIsItsOwnRevision()
    {
        var catalog = NewCatalog();
        await catalog.ImportAsync(
            [Definition(100163, "walk", AchievementSources.FIGURE)],
            true,
            "tests",
            "create",
            "walk-op-0",
            Ct
        );

        await catalog.SetStateAsync(
            "walk",
            AchievementState.Archived,
            true,
            "console",
            "a",
            "walk-op-1",
            Ct
        );
        await catalog.SetStateAsync(
            "walk",
            AchievementState.OffSeason,
            true,
            "console",
            "b",
            "walk-op-2",
            Ct
        );
        await catalog.SetStateAsync(
            "walk",
            AchievementState.Disabled,
            true,
            "console",
            "c",
            "walk-op-3",
            Ct
        );

        catalog.Current.Single().Revision.Should().Be(4);
        catalog.Current.Single().State.Should().Be(AchievementState.Disabled);
    }

    [Fact]
    public async Task EnablingNeedsTheSameBadgeAssetsAnImportDoes()
    {
        var assetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-enable-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(assetDirectory);
        const string badgeCode = "ACH_EnableMe1";
        var texts = new Dictionary<string, string>
        {
            ["badge_name_" + badgeCode] = "Enable me",
            ["badge_desc_" + badgeCode] = "Enable me",
        };
        var catalog = NewCatalog(
            new AchievementConfig { BadgeAssetDirectory = assetDirectory },
            texts
        );
        var disabled = Definition(100164, "enable-me", AchievementSources.FIGURE) with
        {
            Levels = [new() { Requirement = 1, BadgeCode = badgeCode }],
        };
        await catalog.ImportAsync([disabled], true, "tests", "create", "enable-op-0", Ct);

        var withoutAsset = () =>
            catalog.SetStateAsync(
                "enable-me",
                AchievementState.Enabled,
                true,
                "console",
                "go",
                "enable-op-1",
                Ct
            );
        await withoutAsset.Should().ThrowAsync<InvalidOperationException>();
        catalog.Current.Single().State.Should().Be(AchievementState.Disabled);

        File.WriteAllBytes(Path.Combine(assetDirectory, badgeCode + ".png"), []);
        await catalog.SetStateAsync(
            "enable-me",
            AchievementState.Enabled,
            true,
            "console",
            "go",
            "enable-op-2",
            Ct
        );

        catalog.Current.Single().State.Should().Be(AchievementState.Enabled);
        Directory.Delete(assetDirectory, recursive: true);
    }

    [Fact]
    public async Task AnUnhookedAchievementCanBeListedOrArchivedButNeverEnabled()
    {
        var catalog = NewCatalog();
        var unhooked = Definition(100170, "unhooked", AchievementSources.UNHOOKED);
        await catalog.ImportAsync([unhooked], true, "tests", "ship", "unhook-op-0", Ct);

        foreach (var allowed in new[] { AchievementState.Archived, AchievementState.OffSeason })
        {
            var next = unhooked with { Revision = unhooked.Revision + 1, State = allowed };
            await catalog
                .Invoking(c => c.ImportAsync([next], false, "tests", "ok", "unhook-op-1", Ct))
                .Should()
                .NotThrowAsync();
        }
        var enable = unhooked with { Revision = 2, State = AchievementState.Enabled };
        var import = () => catalog.ImportAsync([enable], false, "tests", "no", "unhook-op-2", Ct);

        await import
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*nothing records*");
    }

    [Fact]
    public async Task AnUnhookedAchievementMayBeMovedToARealSourceButAHookedOneMayNotMoveAgain()
    {
        var catalog = NewCatalog();
        var unhooked = Definition(100171, "move-me", AchievementSources.UNHOOKED);
        await catalog.ImportAsync([unhooked], true, "tests", "ship", "move-op-0", Ct);

        var hooked = unhooked with
        {
            Revision = 2,
            Source = AchievementSources.VISIT,
            Reducer = AchievementReducer.Distinct,
        };
        await catalog.ImportAsync([hooked], true, "tests", "hook", "move-op-1", Ct);

        catalog.Current.Single().Source.Should().Be(AchievementSources.VISIT);
        catalog.Current.Single().Reducer.Should().Be(AchievementReducer.Distinct);
        var again = hooked with
        {
            Revision = 3,
            Source = AchievementSources.PETS,
            Reducer = AchievementReducer.Maximum,
        };
        var import = () => catalog.ImportAsync([again], false, "tests", "move", "move-op-2", Ct);
        await import
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*reinterpreted*");
    }

    [Fact]
    public async Task AHookedAchievementCanNeverBeMovedBackToThePlaceholder()
    {
        var catalog = NewCatalog();
        var hooked = Definition(100172, "stay-hooked", AchievementSources.FIGURE);
        await catalog.ImportAsync([hooked], true, "tests", "ship", "back-op-0", Ct);

        var back = hooked with { Revision = 2, Source = AchievementSources.UNHOOKED };
        var import = () => catalog.ImportAsync([back], false, "tests", "back", "back-op-1", Ct);

        await import
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*reinterpreted*");
    }

    private AchievementCatalog NewCatalog(
        AchievementConfig? config = null,
        IReadOnlyDictionary<string, string>? texts = null,
        IAchievementPackRegistry? packs = null
    )
    {
        var dictionary = texts ?? new Dictionary<string, string>();
        HotelTextFakes.Use(_fakes, key => dictionary.GetValueOrDefault(key));
        return new AchievementCatalog(
            _db,
            _fakes.Create<ICurrencyTypeProvider>(),
            Options.Create(config ?? new AchievementConfig()),
            _fakes.Create<IHotelTextProvider>(),
            logger: null,
            packs: packs
        );
    }

    private static AchievementDefinition Definition(int id, string key, string source) =>
        new()
        {
            Id = id,
            Key = key,
            Revision = 1,
            Category = "identity",
            Source = source,
            Reducer = AchievementReducer.Counter,
            State = AchievementState.Disabled,
            Levels = [new() { Requirement = 1, BadgeCode = $"ACH_{key.Replace('-', '_')}1" }],
        };
}
