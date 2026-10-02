using FluentAssertions;
using Turbo.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

public sealed class AchievementPackRegistryTests
{
    private static AchievementDefinition Definition(int id, string key) =>
        new()
        {
            Id = id,
            Key = key,
            Revision = 1,
            Category = "christmas",
            Source = AchievementSources.LOGIN,
            Reducer = AchievementReducer.Counter,
            State = AchievementState.Disabled,
            Levels = [new() { Requirement = 1, BadgeCode = $"ACH_{key}1" }],
        };

    private static TestAchievementPack Pack(
        string key = "xmas",
        int version = 1,
        AchievementIdRange[]? ranges = null,
        params AchievementDefinition[] definitions
    ) =>
        new(
            key,
            version,
            ranges ?? [new(2000, 2099)],
            definitions.Length > 0 ? definitions : [Definition(2001, "carol")]
        );

    [Fact]
    public void AValidPackRegistersAndItsDisposalRemovesIt()
    {
        var registry = new AchievementPackRegistry([]);

        var registration = registry.Register(Pack());

        registry.Packs.Should().ContainSingle().Which.Key.Should().Be("xmas");
        registration.Dispose();
        registry.Packs.Should().BeEmpty();
        registry.Register(Pack()).Should().NotBeNull();
    }

    [Fact]
    public void ThePacksTheContainerProvidesAreRegisteredAtStartup() =>
        new AchievementPackRegistry([
            Pack("one", 1, [new(2000, 2009)], Definition(2001, "a")),
            Pack("two", 1, [new(2010, 2019)], Definition(2011, "b")),
        ])
            .Packs.Select(x => x.Key)
            .Should()
            .Equal("one", "two");

    [Fact]
    public void TheShippedHabboPackIsValid() =>
        new AchievementPackRegistry([new HabboAchievementPack()]).Packs.Should().ContainSingle();

    [Fact]
    public void ARepeatedKeyIsRejected()
    {
        var registry = new AchievementPackRegistry([Pack()]);

        var register = () =>
            registry.Register(
                Pack(ranges: [new(3000, 3099)], definitions: Definition(3001, "other"))
            );

        register.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Xmas", 1)]
    [InlineData("", 1)]
    [InlineData("1xmas", 1)]
    [InlineData("xmas", 0)]
    [InlineData("xmas", -1)]
    public void ABadKeyOrVersionIsRejected(string key, int version)
    {
        var registry = new AchievementPackRegistry([]);

        var register = () => registry.Register(Pack(key, version));

        register.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(999, 1500)]
    [InlineData(1, 1100)]
    [InlineData(2000, 1999)]
    [InlineData(2000, 100000)]
    [InlineData(99999, 150000)]
    public void ARangeBelowThePackStartAboveItOrBackwardsIsRejected(int first, int last)
    {
        var registry = new AchievementPackRegistry([]);

        var register = () =>
            registry.Register(
                Pack(ranges: [new(first, last)], definitions: Definition(first, "edge"))
            );

        register.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AnEmptyRangeListIsRejected()
    {
        var registry = new AchievementPackRegistry([]);

        var register = () => registry.Register(Pack(ranges: []));

        register.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ARangeAnotherPackOwnsIsRejectedEvenWhenItOnlyTouches()
    {
        var registry = new AchievementPackRegistry([
            Pack("one", 1, [new(2000, 2099)], Definition(2001, "a")),
        ]);

        var overlapping = () =>
            registry.Register(Pack("two", 1, [new(2099, 2199)], Definition(2100, "b")));
        var adjacent = () =>
            registry.Register(Pack("three", 1, [new(2100, 2199)], Definition(2101, "c")));

        overlapping.Should().Throw<ArgumentException>();
        adjacent.Should().NotThrow();
    }

    [Fact]
    public void RangesOfOnePackMayNotOverlapEachOther()
    {
        var registry = new AchievementPackRegistry([]);

        var register = () => registry.Register(Pack(ranges: [new(2000, 2050), new(2040, 2099)]));

        register.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ADefinitionOutsideThePacksRangesIsRejected()
    {
        var registry = new AchievementPackRegistry([]);

        var register = () => registry.Register(Pack(definitions: Definition(2500, "stray")));

        register.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ARepeatedIdOrKeyInsideOnePackIsRejected()
    {
        var registry = new AchievementPackRegistry([]);

        var sameId = () =>
            registry.Register(
                Pack("ids", definitions: [Definition(2001, "a"), Definition(2001, "b")])
            );
        var sameKey = () =>
            registry.Register(
                Pack("keys", definitions: [Definition(2001, "a"), Definition(2002, "A")])
            );

        sameId.Should().Throw<ArgumentException>();
        sameKey.Should().Throw<ArgumentException>();
    }
}
