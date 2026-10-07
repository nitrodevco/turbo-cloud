using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Gamedata;
using Turbo.Gamedata;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Figures;
using Turbo.Gamedata.History;
using Turbo.Primitives.Figures;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// Habbo's figure data taken in record by record and field by field, written as the client loads
/// it, and the rules a figure is fitted by: club, sale, gender, selection and what must be worn.
/// </summary>
public sealed class GamedataFigureTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private const string FILE = """
        <?xml version="1.0" encoding="UTF-8"?>
        <figuredata>
          <colors>
            <palette id="1">
              <color id="1" index="0" club="0" selectable="1">FFCB98</color>
              <color id="2" index="1" club="2" selectable="1">543d35</color>
              <color id="3" index="2" club="0" selectable="0">000000</color>
            </palette>
            <palette id="3">
              <color id="10" index="0" club="0" selectable="1">FFFFFF</color>
              <color id="11" index="1" club="2" selectable="1">#96743D</color>
            </palette>
          </colors>
          <sets>
            <settype type="hd" paletteid="1" mand_m_0="1" mand_f_0="1" mand_m_1="1" mand_f_1="1">
              <set id="180" gender="M" club="0" colorable="1" selectable="1" preselectable="1">
                <part id="1" type="hd" colorable="1" index="0" colorindex="1"/>
              </set>
              <set id="600" gender="F" club="0" colorable="1" selectable="1" preselectable="1">
                <part id="1" type="hd" colorable="1" index="0" colorindex="1"/>
              </set>
            </settype>
            <settype type="ch" paletteid="3" mand_m_0="1" mand_f_0="1" mand_m_1="0" mand_f_1="1">
              <set id="210" gender="U" club="0" colorable="1" selectable="1" preselectable="0" sellable="0">
                <part id="210" type="ch" colorable="1" index="0" colorindex="1"/>
                <part id="210" type="ls" colorable="1" index="0" colorindex="2"/>
                <hiddenlayers><layer parttype="cp"/></hiddenlayers>
              </set>
              <set id="211" gender="U" club="2" colorable="1" selectable="1" preselectable="0">
                <part id="211" type="ch" colorable="1" index="0" colorindex="1"/>
              </set>
              <set id="212" gender="U" club="0" colorable="0" selectable="1" preselectable="0" sellable="1">
                <part id="212" type="ch" colorable="0" index="0" colorindex="0"/>
              </set>
              <set id="213" gender="U" club="0" colorable="0" selectable="0" preselectable="0">
                <part id="213" type="ch" colorable="0" index="0" colorindex="0"/>
              </set>
              <set id="bad" gender="U"/>
            </settype>
            <settype type="ha" paletteid="3" mand_m_0="0" mand_f_0="0" mand_m_1="0" mand_f_1="0">
              <set id="1001" gender="U" club="0" colorable="0" selectable="1" preselectable="0">
                <part id="1" type="ha" colorable="0" index="0" colorindex="0"/>
              </set>
            </settype>
          </sets>
        </figuredata>
        """;

    private static readonly FigureData DATA = FigureDataProvider.Build(
        FigureDataFile.Parse(Encoding.UTF8.GetBytes(FILE))
    );

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly GamedataFigureService _figures;
    private readonly GamedataHistoryService _history;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GamedataFigureTests()
    {
        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();
        var figureData = _fakes.Create<IFigureDataProvider>();

        _figures = new GamedataFigureService(
            _db,
            config,
            files,
            figureData,
            writes,
            TimeProvider.System,
            NullLogger<GamedataFigureService>.Instance
        );
        _history = new GamedataHistoryService(
            _db,
            config,
            _fakes.Create<IFurnitureDefinitionProvider>(),
            files,
            writes,
            _fakes.Create<IHotelTextProvider>(),
            figureData,
            NullLogger<GamedataHistoryService>.Instance
        );
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void the_file_reads_every_record_it_can_and_writes_as_the_client_loads_it()
    {
        var records = FigureDataFile.Parse(Encoding.UTF8.GetBytes(FILE));

        records.Count(x => x.Kind == FigureRecordKind.Color).Should().Be(5);
        records.Count(x => x.Kind == FigureRecordKind.SetType).Should().Be(3);
        // The piece without an id doesn't read.
        records.Count(x => x.Kind == FigureRecordKind.Set).Should().Be(7);

        using var json = JsonDocument.Parse(FigureDataFile.Write(records));
        var root = json.RootElement;

        root.GetProperty("palettes")[1]
            .GetProperty("colors")[1]
            .GetProperty("hexCode")
            .GetString()
            .Should()
            .Be("96743D");

        var ch = root.GetProperty("setTypes")[1];

        ch.GetProperty("type").GetString().Should().Be("ch");
        ch.GetProperty("paletteId").GetInt32().Should().Be(3);
        ch.GetProperty("mandatory_m_1").GetBoolean().Should().BeFalse();
        ch.GetProperty("mandatory_f_1").GetBoolean().Should().BeTrue();

        var set = ch.GetProperty("sets")[0];

        set.GetProperty("id").GetInt32().Should().Be(210);
        set.GetProperty("sellable").GetBoolean().Should().BeFalse();
        set.GetProperty("parts")[1].GetProperty("colorindex").GetInt32().Should().Be(2);
        set.GetProperty("hiddenLayers")[0].GetProperty("partType").GetString().Should().Be("cp");
        // A piece with neither parts nor hidden layers leaves both out, as the converter does.
        ch.GetProperty("sets")[2].TryGetProperty("hiddenLayers", out _).Should().BeFalse();
    }

    [Theory]
    // Anyone may wear what everyone has.
    [InlineData("hd-180-1.ch-210-10", 0, "hd-180-1.ch-210-10")]
    // A club colour comes off a player outside the club: the first they may have instead.
    [InlineData("hd-180-2.ch-210-10", 0, "hd-180-1.ch-210-10")]
    [InlineData("hd-180-2.ch-210-10", 2, "hd-180-2.ch-210-10")]
    // A club piece too; the kind is mandatory, so the first they may wear goes on.
    [InlineData("hd-180-1.ch-211-10", 0, "hd-180-1.ch-210")]
    [InlineData("hd-180-1.ch-211-11", 2, "hd-180-1.ch-211-11")]
    // Mandatory for club members is the club column: ch isn't, for a man.
    [InlineData("hd-180-1", 2, "hd-180-1")]
    [InlineData("hd-180-1", 0, "hd-180-1.ch-210")]
    // A colour that can't be chosen, and colours past the piece's layers.
    [InlineData("hd-180-3.ch-210-10-11-10", 0, "hd-180-1.ch-210-10-10")]
    // A piece that takes no colour keeps none; one not offered comes off.
    [InlineData("hd-180-1.ch-210.ha-1001-10", 0, "hd-180-1.ch-210.ha-1001")]
    [InlineData("hd-180-1.ch-213", 0, "hd-180-1.ch-210")]
    // What the figure data doesn't know, a kind worn twice, a piece of another gender.
    [InlineData("hd-180-1.ch-210.xx-1-1.ch-211.hd-999", 2, "hd-180-1.ch-210")]
    [InlineData("hd-600-1.ch-210", 0, "ch-210.hd-180")]
    public void a_figure_keeps_only_what_the_wearer_may_wear(
        string figure,
        int clubLevel,
        string fitted
    ) =>
        FigureRules
            .Fit(DATA, figure, AvatarGenderType.Male, clubLevel, ImmutableHashSet<int>.Empty)
            .Should()
            .Be(fitted);

    [Fact]
    public void a_piece_for_sale_is_worn_only_by_its_owner()
    {
        FigureRules
            .Fit(DATA, "hd-180-1.ch-212", AvatarGenderType.Male, 2, ImmutableHashSet<int>.Empty)
            .Should()
            .Be("hd-180-1");
        FigureRules
            .Fit(DATA, "hd-180-1.ch-212", AvatarGenderType.Male, 0, ImmutableHashSet.Create(212))
            .Should()
            .Be("hd-180-1.ch-212");
    }

    [Fact]
    public void without_figure_data_a_figure_is_worn_as_given() =>
        FigureRules
            .Fit(
                FigureData.Empty,
                "zz-1-1",
                AvatarGenderType.Female,
                0,
                ImmutableHashSet<int>.Empty
            )
            .Should()
            .Be("zz-1-1");

    [Fact]
    public async Task habbos_figure_data_is_taken_in_and_the_hotels_changes_stay()
    {
        await _figures.ImportAsync(Version(1, FILE), STAFF, Ct);

        (await CountAsync(FigureRecordKind.Set)).Should().Be(7);

        // The hotel sells a piece Habbo gives away, and Habbo makes another club-only.
        var own = Set(210);

        own[FigureRecords.SELLABLE] = true;
        await _figures.SaveAsync(FigureRecordKind.Set, own.ToJsonString(), STAFF, Ct);

        var next = Version(
            2,
            FILE.Replace(
                    """<set id="210" gender="U" club="0" colorable="1" selectable="1" preselectable="0" sellable="0">""",
                    """<set id="210" gender="U" club="2" colorable="1" selectable="1" preselectable="0" sellable="0">"""
                )
                .Replace(
                    """<set id="1001" gender="U" club="0" """,
                    """<set id="1001" gender="U" club="2" """
                )
        );
        var preview = await _figures.PreviewImportAsync(next, Ct);

        preview!.Updated.Should().Be(2);
        preview
            .Items.Single(x => x.Key == "210")
            .Fields.Should()
            .ContainSingle(x => x.Field == "club" && !x.Kept);

        await _figures.ImportAsync(next, STAFF, Ct);

        var set = Set(210, await DataAsync(FigureRecordKind.Set, "210"));

        set[FigureRecords.CLUB]!.GetValue<int>().Should().Be(2);
        set[FigureRecords.SELLABLE]!.GetValue<bool>().Should().BeTrue();
        Set(1001, await DataAsync(FigureRecordKind.Set, "1001"))[FigureRecords.CLUB]!
            .GetValue<int>()
            .Should()
            .Be(2);
    }

    [Fact]
    public async Task an_edit_rolls_back_and_a_removed_record_comes_back()
    {
        await _figures.ImportAsync(Version(1, FILE), STAFF, Ct);

        var color = FigureRecords.Parse((await DataAsync(FigureRecordKind.Color, "3/11"))!);

        color[FigureRecords.CLUB] = 0;
        await _figures.SaveAsync(FigureRecordKind.Color, color.ToJsonString(), STAFF, Ct);
        (await _figures.DeleteAsync(FigureRecordKind.SetType, "ha", STAFF, Ct)).Should().BeTrue();

        foreach (var set in (await _history.ListAsync(0, Ct)).Take(2))
            await _history.RollbackAsync(set.Id, STAFF, Ct);

        FigureRecords.Parse((await DataAsync(FigureRecordKind.Color, "3/11"))!)[FigureRecords.CLUB]!
            .GetValue<int>()
            .Should()
            .Be(2);
        (await DataAsync(FigureRecordKind.SetType, "ha")).Should().NotBeNull();
    }

    [Fact]
    public async Task kinds_count_their_pieces_and_pieces_are_found_by_their_fields()
    {
        await _figures.ImportAsync(Version(1, FILE), STAFF, Ct);

        var kinds = await _figures.GetKindsAsync(Ct);

        kinds
            .Kinds.Select(x => (x.Entry.Key, x.Pieces))
            .Should()
            .Equal(("hd", 2), ("ch", 4), ("ha", 1));
        kinds.NextSetId.Should().Be(1002);

        async Task<string[]> Found(params string[] has) =>
            [
                .. (
                    await _figures.SearchAsync(FigureRecordKind.Set, "ch", null, has, 0, Ct)
                ).Items.Select(x => x.Key),
            ];

        (await Found("\"club\":1|\"club\":2")).Should().Equal("211");
        (await Found("\"sellable\":true")).Should().Equal("212");
        // Hidden is the piece's own flag, never preselectable's.
        (await Found("\"selectable\":false"))
            .Should()
            .Equal("213");
        (await Found("\"gender\":\"U\"", "\"club\":0")).Should().Equal("210", "212", "213");
    }

    [Fact]
    public async Task a_palette_is_read_in_order_and_saved_as_one_change_set()
    {
        await _figures.ImportAsync(Version(1, FILE), STAFF, Ct);

        var palettes = await _figures.GetPalettesAsync(Ct);

        palettes.Select(x => x.Id).Should().Equal(1, 3);
        palettes[0].UsedBy.Should().Equal("hd");
        palettes[1].UsedBy.Should().Equal("ch", "ha");
        palettes[0].Colors.Select(x => x.Key).Should().Equal("1/1", "1/2", "1/3");

        // Reversed, one recoloured, one removed and one added: one change set.
        var colors = palettes[0].Colors.Select(x => FigureRecords.Parse(x.Data)).ToList();

        colors[0][FigureRecords.INDEX] = 2;
        colors[2][FigureRecords.INDEX] = 0;
        colors[2][FigureRecords.HEX] = "123456";

        var added = new JsonObject
        {
            ["palette"] = 1,
            ["id"] = 50,
            ["index"] = 1,
            ["club"] = 0,
            ["selectable"] = true,
            ["hex"] = "ABCDEF",
        };
        var changed = await _figures.SaveBatchAsync(
            FigureRecordKind.Color,
            [colors[0].ToJsonString(), colors[2].ToJsonString(), added.ToJsonString()],
            ["1/2"],
            "Reordered palette 1",
            STAFF,
            Ct
        );

        changed.Should().Be(4);
        (await _history.ListAsync(0, Ct)).First().Summary.Should().Be("Reordered palette 1");
        (await _figures.GetPalettesAsync(Ct))[0]
            .Colors.Select(x => x.Key)
            .Should()
            .Equal("1/3", "1/50", "1/1");

        var bad = () =>
            _figures.SaveBatchAsync(
                FigureRecordKind.Color,
                ["""{"palette":1,"id":9,"hex":"nope"}"""],
                [],
                "",
                STAFF,
                Ct
            );

        await bad.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(FigureRecordKind.Color, """{"palette":1,"id":4,"hex":"nothex"}""")]
    [InlineData(FigureRecordKind.Set, """{"id":5,"type":"ch","gender":"X"}""")]
    [InlineData(FigureRecordKind.Set, """{"id":5,"type":"CH!","gender":"U"}""")]
    [InlineData(FigureRecordKind.SetType, """{"type":"hr","paletteid":-1}""")]
    [InlineData(FigureRecordKind.Set, """not json""")]
    public async Task a_record_that_doesnt_make_one_is_refused(FigureRecordKind kind, string data)
    {
        var save = () => _figures.SaveAsync(kind, data, STAFF, Ct);

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private static JsonObject Set(int id, string? data = null) =>
        data is not null
            ? FigureRecords.Parse(data)
            : FigureDataFile
                .Parse(Encoding.UTF8.GetBytes(FILE))
                .Single(x =>
                    x.Kind == FigureRecordKind.Set && x.Record["id"]!.GetValue<int>() == id
                )
                .Record;

    private async Task<int> CountAsync(FigureRecordKind kind)
    {
        await using var dbCtx = _db.CreateDbContext();

        return await dbCtx.GamedataFigures.CountAsync(x => x.Kind == kind, Ct);
    }

    private async Task<string?> DataAsync(FigureRecordKind kind, string key)
    {
        await using var dbCtx = _db.CreateDbContext();

        return (
            await dbCtx
                .GamedataFigures.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Kind == kind && x.Key == key, Ct)
        )?.Data;
    }

    private int Version(int id, string xml)
    {
        var data = Encoding.UTF8.GetBytes(xml);

        _db.Insert(
            new HabboFigureVersionEntity
            {
                Id = id,
                Domain = "com",
                Hash = GamedataBytes.Hash(data),
                Content = GamedataBytes.Compress(data),
                SetCount = 7,
                ColorCount = 5,
                CheckedAt = DateTime.UtcNow,
            }
        );

        return id;
    }
}
