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
    public async Task ShippedDefaultsInstallIntoAnEmptyHotelOnceAndNeverOverwriteIt()
    {
        var assetDirectory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-defaults-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(assetDirectory);
        var texts = new Dictionary<string, string>();
        foreach (
            var code in AchievementDefaults
                .Definitions.SelectMany(x => x.Levels)
                .Select(x => x.BadgeCode)
        )
        {
            File.WriteAllBytes(Path.Combine(assetDirectory, code + ".png"), []);
            texts["badge_name_" + code] = code;
            texts["badge_desc_" + code] = code;
        }
        var catalog = NewCatalog(
            new AchievementConfig { BadgeAssetDirectory = assetDirectory },
            texts
        );

        await catalog.ReloadAsync(Ct);
        await catalog.ReloadAsync(Ct);

        catalog.Current.Should().HaveCount(AchievementDefaults.Definitions.Length);
        await using var db = await _db.CreateDbContextAsync(Ct);
        (await db.AchievementDefinitions.CountAsync(Ct))
            .Should()
            .Be(AchievementDefaults.Definitions.Length);
        (await db.AchievementAudit.SingleAsync(Ct))
            .OperationId.Should()
            .Be(AchievementCatalog.DefaultsOperationId);
        Directory.Delete(assetDirectory, recursive: true);
    }

    [Fact]
    public async Task DefaultsAreNotInstalledOverAnExistingCatalogOrWhenDisabled()
    {
        var existing = NewCatalog(new AchievementConfig { InstallDefaults = false });
        await existing.ReloadAsync(Ct);
        existing.Current.Should().BeEmpty();

        var custom = Definition(100100, "hotel-owned", AchievementSources.FIGURE);
        _db.Insert(
            new AchievementDefinitionEntity
            {
                AchievementId = custom.Id,
                Revision = custom.Revision,
                DefinitionJson = JsonSerializer.Serialize(custom),
            }
        );
        var catalog = NewCatalog();
        await catalog.ReloadAsync(Ct);
        catalog.Current.Should().ContainSingle().Which.Id.Should().Be(100100);
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

    private AchievementCatalog NewCatalog(
        AchievementConfig? config = null,
        IReadOnlyDictionary<string, string>? texts = null
    )
    {
        var dictionary = texts ?? new Dictionary<string, string>();
        _fakes.Handlers[nameof(IHotelTextProvider.TryGetText)] = call =>
        {
            var key = (string)call.Args[0]!;
            if (dictionary.TryGetValue(key, out var value))
            {
                call.Args[1] = value;
                return true;
            }
            call.Args[1] = string.Empty;
            return false;
        };
        return new AchievementCatalog(
            _db,
            _fakes.Create<ICurrencyTypeProvider>(),
            Options.Create(config ?? new AchievementConfig()),
            _fakes.Create<IHotelTextProvider>()
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
