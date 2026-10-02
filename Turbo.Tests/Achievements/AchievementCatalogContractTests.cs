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
                Enabled = true,
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
                Enabled = true,
                Levels = [new() { Requirement = 1, BadgeCode = "ACH_RespectGiven1" }],
            };
            var numericBase = Definition(
                100121,
                "registration-text",
                AchievementSources.ACCOUNT_AGE
            ) with
            {
                Enabled = true,
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
            Enabled = false,
            Levels = [new() { Requirement = 1, BadgeCode = $"ACH_{key.Replace('-', '_')}1" }],
        };
}
