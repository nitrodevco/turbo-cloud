using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.History;
using Turbo.Gamedata.Texts;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's external texts taken in, read and written as Habbo's file and the client read them,
/// with the hotel's own changes kept across Habbo's updates and rolled back as a set.
/// </summary>
public sealed class GamedataTextTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataTextService _texts;
    private readonly GamedataHistoryService _history;
    private readonly HotelTextProvider _hotelTexts;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataTextTests()
    {
        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();

        _hotelTexts = new HotelTextProvider(_db, config, TimeProvider.System);

        _texts = new GamedataTextService(
            _db,
            config,
            files,
            writes,
            TimeProvider.System,
            _hotelTexts,
            NullLogger<GamedataTextService>.Instance
        );
        _history = new GamedataHistoryService(
            _db,
            config,
            _fakes.Create<IFurnitureDefinitionProvider>(),
            files,
            writes,
            _hotelTexts,
            _fakes.Create<Turbo.Primitives.Figures.IFigureDataProvider>(),
            NullLogger<GamedataHistoryService>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void the_file_reads_as_the_client_reads_it_and_writes_back_the_same()
    {
        const string FILE =
            "b.title=Hello = world\n# a comment\n\r\na.text=Line one\\nLine two\nnokey\n=novalue\nb.title=Last wins\n";

        var texts = ExternalTextsFile.Parse(FILE);

        texts.Select(x => x.Key).Should().Equal("b.title", "a.text");
        texts["b.title"].Should().Be("Last wins");
        texts["a.text"].Should().Be("Line one\\nLine two");
        Encoding
            .UTF8.GetString(ExternalTextsFile.Write(texts.Select(x => (x.Key, x.Value))))
            .Should()
            .Be("a.text=Line one\\nLine two\nb.title=Last wins\n");
    }

    [Theory]
    [InlineData("new", null, null, FurnitureImportAction.Add)]
    [InlineData("same", "same", "old", null)]
    [InlineData("new", "old", "old", FurnitureImportAction.Update)]
    [InlineData("new", "mine", "old", FurnitureImportAction.Keep)]
    [InlineData("old", "mine", "old", null)]
    [InlineData("new", null, "old", FurnitureImportAction.Keep)]
    [InlineData("old", null, "old", null)]
    [InlineData("new", "mine", null, FurnitureImportAction.Keep)]
    public void each_key_is_compared_three_ways(
        string habbo,
        string? ours,
        string? previous,
        FurnitureImportAction? expected
    ) => GamedataTextService.Decide(habbo, ours, previous).Should().Be(expected);

    [Fact]
    public async Task habbos_texts_are_taken_in_and_its_changes_reach_only_what_the_hotel_left_alone()
    {
        await _texts.ImportAsync(Version(1, "a=One\nb=Two\nc=Three\n"), STAFF, Ct);

        (await TextsAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["a"] = "One",
                    ["b"] = "Two",
                    ["c"] = "Three",
                }
            );

        await _texts.SaveAsync("b", "Mine", STAFF, Ct);
        await _texts.DeleteAsync("c", STAFF, Ct);
        await _texts.SaveAsync("custom.key", "Ours alone", STAFF, Ct);

        var next = Version(2, "a=One again\nb=Two again\nc=Three again\nd=Four\n");
        var preview = await _texts.PreviewImportAsync(next, Ct);

        preview!.Added.Should().Be(1);
        preview.Updated.Should().Be(1);
        preview.Kept.Should().Be(2);

        await _texts.ImportAsync(next, STAFF, Ct);

        (await TextsAsync())
            .Should()
            .BeEquivalentTo(
                new Dictionary<string, string>
                {
                    ["a"] = "One again",
                    ["b"] = "Mine",
                    ["d"] = "Four",
                    ["custom.key"] = "Ours alone",
                }
            );
    }

    [Fact]
    public async Task keys_that_differ_only_in_case_are_two_texts()
    {
        await _texts.ImportAsync(
            Version(
                1,
                "ACH_PinataBreaker1_badge_name=Pinata\nACH_pinatabreaker1_badge_name=pinata\n"
            ),
            STAFF,
            Ct
        );

        (await TextsAsync()).Should().HaveCount(2);
    }

    [Fact]
    public async Task an_import_rolls_back_as_a_whole_and_an_edit_after_it_stays()
    {
        await _texts.ImportAsync(Version(1, "a=One\n"), STAFF, Ct);

        var second = await _texts.ImportAsync(Version(2, "a=Uno\nb=Dos\n"), STAFF, Ct);

        await _texts.SaveAsync("b", "Mine", STAFF, Ct);

        var result = await _history.RollbackAsync(second!.Id, STAFF, Ct);

        result!.Skipped.Should().ContainSingle(x => x.StartsWith("b:"));
        (await TextsAsync())
            .Should()
            .BeEquivalentTo(new Dictionary<string, string> { ["a"] = "One", ["b"] = "Mine" });
    }

    [Fact]
    public async Task a_removed_text_comes_back_when_its_removal_is_rolled_back()
    {
        await _texts.SaveAsync("greeting", "Hello", STAFF, Ct);
        await _texts.DeleteAsync("greeting", STAFF, Ct);

        var removal = (await _history.ListAsync(0, Ct)).First();

        await _history.RollbackAsync(removal.Id, STAFF, Ct);

        (await TextsAsync()).Should().Contain("greeting", "Hello");
    }

    [Fact]
    public async Task the_hotel_reads_only_the_keys_it_asks_for_with_references_followed()
    {
        await _texts.ImportAsync(
            Version(
                1,
                "widget.memenu.dance1=Hab-Hop\nwiredfurni.params.action.dance.1=${widget.memenu.dance1}\nloop.a=${loop.b}\nloop.b=${loop.a}\nnowhere=${missing.key}\nmixed=Hi ${widget.memenu.dance1}\n"
            ),
            STAFF,
            Ct
        );

        var texts = await _hotelTexts.GetTextsAsync(
            ["wiredfurni.params.action.dance.1", "loop.a", "nowhere", "mixed", "absent"],
            Ct
        );

        texts.Count.Should().Be(4);
        texts.TryGetText("wiredfurni.params.action.dance.1", out var dance).Should().BeTrue();
        dance.Should().Be("Hab-Hop");
        texts.TryGetText("loop.a", out var loop).Should().BeTrue();
        loop.Should().StartWith("${loop.");
        texts.TryGetText("nowhere", out var nowhere).Should().BeTrue();
        nowhere.Should().Be("${missing.key}");
        texts.TryGetText("mixed", out var mixed).Should().BeTrue();
        mixed.Should().Be("Hi ${widget.memenu.dance1}");
        texts.TryGetText("absent", out _).Should().BeFalse();
        (await _hotelTexts.GetTextAsync("absent", Ct)).Should().BeNull();
    }

    [Fact]
    public async Task a_family_is_every_key_with_its_prefix_and_nothing_else()
    {
        await _texts.ImportAsync(
            Version(
                1,
                "fx_1=Glow\nfx_2=${fx_1}\nfx_2_desc=Shines\nfx=Not one\nFX_3=Other case\nhanditem1=Tea\n"
            ),
            STAFF,
            Ct
        );

        var family = await _hotelTexts.GetTextsByPrefixAsync("fx_", Ct);

        family.Count.Should().Be(3);
        family.TryGetText("fx_2", out var two).Should().BeTrue();
        two.Should().Be("Glow");
        family.TryGetText("FX_3", out _).Should().BeFalse();
        family.TryGetText("handitem1", out _).Should().BeFalse();
    }

    [Fact]
    public async Task an_edit_or_rollback_is_read_at_once_despite_what_was_kept()
    {
        await _texts.SaveAsync("greeting", "Hello", STAFF, Ct);
        (await _hotelTexts.GetTextAsync("greeting", Ct)).Should().Be("Hello");
        (await _hotelTexts.GetTextsByPrefixAsync("greet", Ct)).Count.Should().Be(1);

        await _texts.SaveAsync("greeting", "Hi", STAFF, Ct);
        (await _hotelTexts.GetTextAsync("greeting", Ct)).Should().Be("Hi");

        var edit = (await _history.ListAsync(0, Ct)).First();
        await _history.RollbackAsync(edit.Id, STAFF, Ct);
        (await _hotelTexts.GetTextAsync("greeting", Ct)).Should().Be("Hello");

        await _texts.DeleteAsync("greeting", STAFF, Ct);
        (await _hotelTexts.GetTextAsync("greeting", Ct)).Should().BeNull();
        (await _hotelTexts.GetTextsByPrefixAsync("greet", Ct)).Count.Should().Be(0);
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("a=b", "value")]
    [InlineData("#comment", "value")]
    [InlineData("key", "two\nlines")]
    public async Task a_text_the_file_cant_hold_is_refused(string key, string value)
    {
        var save = () => _texts.SaveAsync(key, value, STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private async Task<Dictionary<string, string>> TextsAsync()
    {
        await using var dbCtx = _db.CreateDbContext();

        return await dbCtx
            .GamedataTexts.AsNoTracking()
            .ToDictionaryAsync(x => x.Key, x => x.Value, Ct);
    }

    private int Version(int id, string texts)
    {
        var data = Encoding.UTF8.GetBytes(texts);

        _db.Insert(
            new HabboTextVersionEntity
            {
                Id = id,
                Domain = "com",
                Hash = GamedataBytes.Hash(data),
                Content = GamedataBytes.Compress(data),
                TextCount = ExternalTextsFile.Parse(texts).Count,
                CheckedAt = DateTime.UtcNow,
            }
        );

        return id;
    }
}
