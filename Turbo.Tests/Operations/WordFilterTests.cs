using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Moderation;
using Turbo.Operations;
using Turbo.Operations.Configuration;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

public sealed class WordFilterTests
{
    private static readonly HashSet<string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        "heck",
        "darn",
    };

    [Theory]
    [InlineData("what the heck", "what the bobba")]
    [InlineData("HECK!", "bobba!")]
    [InlineData("(heck), darn.", "(bobba), bobba.")]
    [InlineData("line one\nheck\tdarn", "line one\nbobba\tbobba")]
    [InlineData("heckle the darned", "heckle the darned")]
    [InlineData("", "")]
    public void AWholeWordIsReplacedWhateverSurroundsIt(string text, string expected) =>
        FilterWords.Apply(text, Words, "bobba").Should().Be(expected);

    [Fact]
    public void TextWithNoListedWordIsReturnedAsItIs()
    {
        const string text = "nothing to see";

        FilterWords.Apply(text, Words, "bobba").Should().BeSameAs(text);
        FilterWords.ContainsAny(text, Words).Should().BeFalse();
        FilterWords.ContainsAny("oh, Darn", Words).Should().BeTrue();
    }

    [Fact]
    public async Task TheHotelFilterReadsItsWordsAndReplacesWithTheConfiguredText()
    {
        var filter = await LoadAsync("heck", " Darn ");

        filter.Filter("heck, DARN and heckle").Should().Be("**, ** and heckle");
        filter.IsClean("all good").Should().BeTrue();
        filter.IsClean("darn it").Should().BeFalse();
    }

    [Fact]
    public async Task ARoomsOwnWordsAreFilteredAlongsideTheHotels()
    {
        var filter = await LoadAsync("heck");
        var roomWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "gosh" };

        filter.Filter("heck and gosh", roomWords).Should().Be("** and **");
    }

    [Fact]
    public async Task AReloadPicksUpWordsAddedSinceTheLast()
    {
        var db = new InMemoryDb();
        var filter = new WordFilter(
            db,
            Options.Create(new OperationsConfig { WordFilterReplacement = "**" }),
            NullLogger<IWordFilter>.Instance
        );

        await filter.ReloadAsync(CancellationToken.None);
        filter.Filter("heck").Should().Be("heck");

        await AddWordsAsync(db, "heck");
        await filter.ReloadAsync(CancellationToken.None);

        filter.Filter("heck").Should().Be("**");
    }

    [Fact]
    public async Task APetNameHoldingAFilteredWordIsForbidden()
    {
        var filter = await LoadAsync("heck");

        PetNames.Validate("Heck Dog", 1, 15, filter).Should().Be(PetNameValidationType.Forbidden);
        PetNames.Validate("Rex", 1, 15, filter).Should().Be(PetNameValidationType.Ok);
    }

    [Fact]
    public async Task FilteringComesBeforeTheCutSoAReplacementCannotOverflow()
    {
        var filter = await LoadAsync("ab");

        filter.FilterAndTruncate("  ab ab  ", 4).Should().Be("** *");
    }

    private static async Task<WordFilter> LoadAsync(params string[] words)
    {
        var db = new InMemoryDb();

        await AddWordsAsync(db, words);

        var filter = new WordFilter(
            db,
            Options.Create(new OperationsConfig { WordFilterReplacement = "**" }),
            NullLogger<IWordFilter>.Instance
        );

        await filter.ReloadAsync(CancellationToken.None);

        return filter;
    }

    private static async Task AddWordsAsync(InMemoryDb db, params string[] words)
    {
        await using var dbCtx = db.CreateDbContext();

        dbCtx.FilterWords.AddRange(words.Select(word => new FilterWordEntity { Word = word }));

        await dbCtx.SaveChangesAsync();
    }
}
