using FluentAssertions;
using Microsoft.Extensions.Options;
using Turbo.Achievements;
using Turbo.Achievements.Configuration;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Achievements;

/// <summary>The files in docs/examples/achievements, run exactly as a hotel owner would use them.</summary>
public sealed class AchievementExampleTests : IDisposable
{
    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    private static string Read(string name)
    {
        using var stream = typeof(AchievementExampleTests).Assembly.GetManifestResourceStream(
            $"Examples.Achievements.{name}.json"
        );
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private AchievementSync NewSync(AchievementConfig config, Dictionary<string, string> texts)
    {
        HotelTextFakes.Use(_fakes, key => texts.GetValueOrDefault(key));
        var options = Options.Create(config);
        var provider = _fakes.Create<IHotelTextProvider>();
        var catalog = new AchievementCatalog(
            _db,
            _fakes.Create<ICurrencyTypeProvider>(),
            options,
            provider
        );

        return new AchievementSync(catalog, provider, options);
    }

    [Theory]
    [InlineData("twelve-days-of-christmas")]
    [InlineData("visit-the-christmas-rooms")]
    public void ExamplesUseNamesRatherThanNumbers(string name)
    {
        var json = Read(name);

        json.Should().Contain("\"Reducer\": \"Distinct\"").And.Contain("\"State\": \"Enabled\"");
        AchievementDefinitionJson.ReadAll(json).Should().ContainSingle();
    }

    [Fact]
    public async Task WithNothingPreparedTheDryRunListsExactlyWhatToAdd()
    {
        var sync = NewSync(new AchievementConfig(), []);

        var report = await sync.SyncAsync(
            AchievementDefinitionJson.ReadAll(Read("twelve-days-of-christmas")),
            apply: false,
            "tests",
            "check",
            "example-dry",
            Ct
        );

        report.Created.Should().Equal("twelve-days-of-christmas");
        report.MissingTexts.Should().Contain("quests.christmas.name=Christmas");
        report
            .MissingTexts.Should()
            .Contain(x => x.StartsWith("badge_name_ACH_TwelveDaysOfChristmas="));
        report
            .MissingTexts.Should()
            .Contain(x => x.StartsWith("badge_desc_ACH_TwelveDaysOfChristmas="));
        report
            .MissingBadgeImages.Should()
            .Equal(
                "ACH_TwelveDaysOfChristmas1.png",
                "ACH_TwelveDaysOfChristmas2.png",
                "ACH_TwelveDaysOfChristmas3.png",
                "ACH_TwelveDaysOfChristmas4.png"
            );
        report
            .Problems.Should()
            .NotBeEmpty("an enabled achievement is refused until its badge images and texts exist");
        report.Applied.Should().BeFalse();
    }

    [Fact]
    public async Task OnceTheTextsAndImagesExistBothExamplesSyncInOneStep()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"achievement-examples-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directory);
        var codes = new[]
        {
            "ACH_TwelveDaysOfChristmas1",
            "ACH_TwelveDaysOfChristmas2",
            "ACH_TwelveDaysOfChristmas3",
            "ACH_TwelveDaysOfChristmas4",
            "ACH_ChristmasRooms1",
            "ACH_ChristmasRooms2",
        };
        foreach (var code in codes)
            File.WriteAllBytes(Path.Combine(directory, code + ".png"), []);
        var sync = NewSync(
            new AchievementConfig { BadgeAssetDirectory = directory },
            new Dictionary<string, string>
            {
                ["quests.christmas.name"] = "Christmas",
                ["badge_name_ACH_TwelveDaysOfChristmas"] = "Twelve Days of Christmas",
                ["badge_desc_ACH_TwelveDaysOfChristmas"] =
                    "Log in on %limit% of the 12 days of Christmas.",
                ["badge_name_ACH_ChristmasRooms"] = "Christmas Rooms",
                ["badge_desc_ACH_ChristmasRooms"] = "Visit %limit% of the Christmas rooms.",
            }
        );
        var file = AchievementDefinitionJson
            .ReadAll(Read("twelve-days-of-christmas"))
            .AddRange(AchievementDefinitionJson.ReadAll(Read("visit-the-christmas-rooms")));

        var report = await sync.SyncAsync(
            file,
            apply: true,
            "tests",
            "christmas",
            "example-apply",
            Ct
        );

        report.Problems.Should().BeEmpty();
        report.MissingTexts.Should().BeEmpty();
        report.MissingBadgeImages.Should().BeEmpty();
        report
            .Created.Should()
            .BeEquivalentTo("twelve-days-of-christmas", "visit-the-christmas-rooms");
        report.Applied.Should().BeTrue();
        Directory.Delete(directory, recursive: true);
    }
}
