using System.Text.Json;
using FluentAssertions;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class HabboAchievementPackTests
{
    private static readonly HabboAchievementPack Pack = new();

    private static Dictionary<string, JsonElement> Snapshot()
    {
        using var stream = typeof(AchievementDefaults).Assembly.GetManifestResourceStream(
            "Turbo.Achievements.Resources.habbo-achievements-2026-10-02.json"
        );
        Assert.NotNull(stream);

        return JsonSerializer
            .Deserialize<JsonElement[]>(stream)!
            .ToDictionary(x => x.GetProperty("achievement").GetProperty("name").GetString()!);
    }

    /// <summary>
    /// The published name behind a definition. A name that ends in a digit has an underscore added
    /// before the level, which is not part of the name; one that really ends in an underscore keeps it.
    /// </summary>
    private static string NameOf(
        AchievementDefinition definition,
        Dictionary<string, JsonElement> snapshot
    )
    {
        var name = AchievementBadgeCodes.BaseOf(definition.Levels[0].BadgeCode)["ACH_".Length..];

        return snapshot.ContainsKey(name) ? name : name[..^1];
    }

    [Fact]
    public void EveryPublishedRecordIsInThePackOrListedAsNotRepresentable()
    {
        var snapshot = Snapshot();
        var shipped = Pack
            .Definitions.Select(x => AchievementBadgeCodes.BaseOf(x.Levels[0].BadgeCode))
            .Select(x => x["ACH_".Length..])
            .ToHashSet();
        var skipped = AchievementDefaults.NotRepresentable.Select(x => x.Name).ToHashSet();

        var missing = snapshot
            .Keys.Where(name =>
                !shipped.Contains(name) && !shipped.Contains(name + "_") && !skipped.Contains(name)
            )
            .ToList();

        missing.Should().BeEmpty();
        skipped.Should().BeEquivalentTo("DailyHotelPresence", "RecycledItems");
    }

    [Fact]
    public void ThePackHoldsTheHandMappedRecordsPlusEveryOtherRepresentableOne()
    {
        AchievementDefaults.Definitions.Should().HaveCount(18);
        AchievementDefaults.Crackables.Should().HaveCount(9);
        AchievementDefaults.Unhooked.Should().HaveCount(141);
        Pack.Definitions.Should().HaveCount(168);
        Pack.Version.Should().Be(3);
    }

    [Fact]
    public void IdsKeysAndBadgeCodesAreUniqueAndInsideThePacksRanges()
    {
        var definitions = Pack.Definitions;

        definitions.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        definitions
            .Select(x => x.Key)
            .Should()
            .OnlyHaveUniqueItems()
            .And.OnlyContain(x => x == x.ToLowerInvariant());
        definitions
            .SelectMany(x => x.Levels)
            .Select(x => x.BadgeCode)
            .Should()
            .OnlyHaveUniqueItems();
        definitions.Should().OnlyContain(x => Pack.IdRanges.Any(r => r.Contains(x.Id)));
        AchievementDefaults
            .Unhooked.Select(x => x.Id)
            .Should()
            .OnlyContain(x => x >= 10003 && x <= 10334);
        new AchievementPackRegistry([Pack]).Packs.Should().ContainSingle();
    }

    [Fact]
    public void EveryOtherRecordKeepsItsPublishedCategoryAndThresholds()
    {
        var snapshot = Snapshot();

        foreach (var definition in AchievementDefaults.Unhooked)
        {
            var published = snapshot[NameOf(definition, snapshot)];

            definition
                .Category.Should()
                .Be(published.GetProperty("achievement").GetProperty("category").GetString());
            definition
                .Levels.Select(x => x.Requirement)
                .Should()
                .Equal(
                    published
                        .GetProperty("levelRequirements")
                        .EnumerateArray()
                        .Select(x => x.GetProperty("requiredScore").GetInt32())
                );
            definition
                .Id.Should()
                .Be(10000 + published.GetProperty("achievement").GetProperty("id").GetInt32());
        }
    }

    [Fact]
    public void RetiredRecordsShipArchivedAndEverythingElseDisabledOnThePlaceholderSource()
    {
        var snapshot = Snapshot();

        foreach (var definition in AchievementDefaults.Unhooked)
        {
            var state = snapshot[NameOf(definition, snapshot)]
                .GetProperty("achievement")
                .GetProperty("state")
                .GetString();

            definition
                .State.Should()
                .Be(state == "ARCHIVED" ? AchievementState.Archived : AchievementState.Disabled);
            definition.Source.Should().Be(AchievementSources.UNHOOKED);
            definition.Reducer.Should().Be(AchievementReducer.Counter);
            definition.Levels.Should().OnlyContain(x => x.Score == 10 && x.Rewards.IsEmpty);
        }
        AchievementDefaults
            .Unhooked.Count(x => x.State == AchievementState.Archived)
            .Should()
            .Be(14);
    }

    [Fact]
    public void TheCrackablesRecordsCrackableFurniRecordAreBoundByName()
    {
        var snapshot = Snapshot();
        var byName = AchievementDefaults.Crackables.ToDictionary(x => NameOf(x, snapshot));

        byName
            .Keys.Should()
            .BeEquivalentTo(
                "PinataWhacker",
                "PinataBreaker",
                "Horticulturist",
                "AdvancedHorticulturist",
                "CreatureRearer",
                "EasterCreatures",
                "Farmer",
                "Restorer",
                "flamingknight"
            );
        byName["PinataWhacker"].Source.Should().Be(AchievementSources.CRACKABLE_HIT);
        byName
            .Where(x => x.Key != "PinataWhacker")
            .Should()
            .OnlyContain(x => x.Value.Source == AchievementSources.CRACKABLE_CRACKED);
        byName
            .Should()
            .OnlyContain(x =>
                x.Value.Match != null
                && x.Value.Match.Values.SequenceEqual(new[] { x.Key.ToLowerInvariant() })
                && x.Value.State == AchievementState.Enabled
                && x.Value.Reducer == AchievementReducer.Counter
            );
        // Not left on the placeholder source as well.
        AchievementDefaults
            .Unhooked.Select(x => NameOf(x, snapshot))
            .Should()
            .NotIntersectWith(byName.Keys);
    }

    [Fact]
    public void ANameEndingInADigitTakesAnUnderscoreBeforeTheLevel()
    {
        var snapshot = Snapshot();
        var byName = AchievementDefaults.Unhooked.ToDictionary(x => NameOf(x, snapshot));

        byName["bazaar17"].Levels[0].BadgeCode.Should().Be("ACH_bazaar17_1");
        byName["Profile1"].Levels[0].BadgeCode.Should().Be("ACH_Profile1_1");
        byName["Profile2"].Levels[0].BadgeCode.Should().Be("ACH_Profile2_1");
        byName["bazaar17"]
            .Levels.Select(x => AchievementBadgeCodes.LevelOf(x.BadgeCode))
            .Should()
            .Equal(Enumerable.Range(1, byName["bazaar17"].Levels.Length));
    }

    [Fact]
    public void TheTwoRecordsThatCannotBeShippedFaithfullySaySoWhy() =>
        AchievementDefaults
            .NotRepresentable.Should()
            .BeEquivalentTo(
                new[]
                {
                    ("DailyHotelPresence", "The record has no levels."),
                    ("RecycledItems", "Its thresholds do not strictly rise."),
                }
            );
}
