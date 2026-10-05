using FluentAssertions;
using Turbo.Primitives.Players;
using Turbo.Web.Accounts;
using Xunit;

namespace Turbo.Tests.Web;

/// <summary>
/// The hotel name offered to someone signing up with Discord: theirs, kept to what Turbo's names
/// can hold, and numbered when it is someone else's.
/// </summary>
public sealed class NameSuggestionTests
{
    [Theory]
    [InlineData("cool.user_", "cool.user_")]
    [InlineData("Cool User!!", "CoolUser!!")]
    [InlineData("@atname", "atname")]
    [InlineData("ab", "ab1")]
    [InlineData("averyveryverylongname", "averyveryverylo")]
    [InlineData("émoji ✨", "moji")]
    public void ADiscordName_IsFittedToTheRules(string discord, string fitted)
    {
        NameSuggestion.Fit(discord).Should().Be(fitted);
        PlayerNames.Check(NameSuggestion.Fit(discord)).Should().BeNull();
    }

    [Fact]
    public void ATakenName_IsNumbered_WithinTheLength()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "averyveryverylo",
            "averyveryverylo1".Substring(0, 15),
        };

        NameSuggestion
            .Suggest(new("averyveryverylongname", null), taken.Contains)
            .Should()
            .Be("averyveryveryl1");
    }

    [Fact]
    public void TheShownNameIsTried_WhenTheUsernameIsTaken() =>
        NameSuggestion.Suggest(new("bob", "Bobby"), name => name == "bob").Should().Be("Bobby");

    [Fact]
    public void NothingUsable_FallsBackToAPlayerName() =>
        NameSuggestion.Suggest(new("✨✨", null), _ => false).Should().Be("Player");
}
