using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;
using Xunit;

namespace Turbo.Tests.Players;

public sealed class PlayerLookTests
{
    private const string SAVED = "hd-180-1.hr-100-61.ch-210-66.lg-270-82.sh-290-80";

    [Fact]
    public void MergeWithNoOverlayKeepsTheBase()
    {
        PlayerLook.MergeParts(SAVED, string.Empty).Should().Be(SAVED);
    }

    [Fact]
    public void MergeWithNoBaseIsTheOverlay()
    {
        PlayerLook.MergeParts(string.Empty, "ch-300-1.lg-5-2").Should().Be("ch-300-1.lg-5-2");
    }

    [Fact]
    public void MergeKeepsPartsTheOverlayDoesNotName()
    {
        PlayerLook
            .MergeParts(SAVED, "ch-3030-110")
            .Should()
            .Be("hd-180-1.hr-100-61.ch-3030-110.lg-270-82.sh-290-80");
    }

    [Fact]
    public void MergeReplacesEveryPartOfAMatchingSetInPlace()
    {
        PlayerLook
            .MergeParts(SAVED, "lg-1-2.ch-3-4")
            .Should()
            .Be("hd-180-1.hr-100-61.ch-3-4.lg-1-2.sh-290-80");
    }

    [Fact]
    public void MergeAppendsSetsTheBaseLacks()
    {
        PlayerLook
            .MergeParts("hd-180-1.ch-210-66", "cc-3-1.ha-1002-70")
            .Should()
            .Be("hd-180-1.ch-210-66.cc-3-1.ha-1002-70");
    }

    [Fact]
    public void MergeIsCaseSensitiveOnSetType()
    {
        PlayerLook.MergeParts("ch-1-1", "CH-2-2").Should().Be("ch-1-1.CH-2-2");
    }

    [Fact]
    public void MergeDropsEmptyPartsAndRepeatedSets()
    {
        PlayerLook.MergeParts("hd-1-1..hd-2-2.", "").Should().Be("hd-1-1");
    }

    [Fact]
    public void MergeKeepsTheLastPartOfASetTheOverlayNamesTwice()
    {
        PlayerLook.MergeParts("ch-1-1", "ch-2-2.ch-3-3").Should().Be("ch-3-3");
    }

    [Fact]
    public void ResolveWithNoOverrideIsTheSavedLook()
    {
        PlayerLook
            .Resolve(SAVED, AvatarGenderType.Female, null)
            .Should()
            .Be((SAVED, AvatarGenderType.Female));
    }

    [Fact]
    public void ResolveReplaceShowsOnlyTheOverrideAndKeepsTheSavedGenderWhenNoneIsNamed()
    {
        var look = new PlayerLookOverrideSnapshot
        {
            Figure = "hd-1-1",
            Mode = LookOverrideMode.Replace,
        };

        PlayerLook
            .Resolve(SAVED, AvatarGenderType.Female, look)
            .Should()
            .Be(("hd-1-1", AvatarGenderType.Female));
    }

    [Fact]
    public void ResolveTakesTheOverrideGenderWhenItNamesOne()
    {
        var look = new PlayerLookOverrideSnapshot
        {
            Figure = "ch-3030-110",
            Gender = AvatarGenderType.Male,
            Mode = LookOverrideMode.MergeParts,
        };

        PlayerLook
            .Resolve(SAVED, AvatarGenderType.Female, look)
            .Should()
            .Be(("hd-180-1.hr-100-61.ch-3030-110.lg-270-82.sh-290-80", AvatarGenderType.Male));
    }

    [Fact]
    public void ResolveRejectsAnUnknownMode()
    {
        var look = new PlayerLookOverrideSnapshot
        {
            Figure = "hd-1-1",
            Mode = (LookOverrideMode)99,
        };

        var act = () => PlayerLook.Resolve(SAVED, AvatarGenderType.Male, look);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
