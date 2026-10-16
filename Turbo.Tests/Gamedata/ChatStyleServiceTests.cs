using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Turbo.Database.Entities.Players;
using Turbo.Gamedata.ChatStyles;
using Turbo.Gamedata.Habbo;
using Turbo.Players.Providers;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Providers;
using Turbo.Tests.Assets;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Gamedata;

/// <summary>
/// The chat bubble styles whole: a habbo-free-flow-chat-com bundle taken in (each style's look and
/// bitmaps, put back to their own size where the bundle's packer trimmed them, and the library's
/// other bitmaps), who may speak with a style kept as the hotel set it, and the bundle built again
/// after every change - untrimmed, in the client's order, published from the bundle store.
/// </summary>
public sealed class ChatStyleServiceTests : IDisposable
{
    private static readonly PlayerId STAFF = new(7);

    private const string NORMAL_POINTS =
        """{"scale9":{"x":29,"y":17,"width":1,"height":1},"faceXY":{"x":13,"y":12},"textFieldMargins":{"x":28,"y":4,"width":12,"height":7},"pointerY":23,"fontSize":12,"fontFace":"Ubuntu","textColorRGB":0}""";

    private readonly SqliteDb _db = new();
    private readonly AssetFolder _folder;
    private readonly ChatStyleProvider _provider;
    private readonly Host _host = new();
    private readonly ChatStyleService _styles;

    public ChatStyleServiceTests()
    {
        _folder = new AssetFolder(_db);
        _provider = new ChatStyleProvider(_db, NullLogger<IChatStyleProvider>.Instance);
        _styles = new ChatStyleService(
            _db,
            _folder.Store,
            _provider,
            new HabboGamedataClient(_host, NullLogger<HabboGamedataClient>.Instance),
            TimeProvider.System,
            NullLogger<ChatStyleService>.Instance
        );

        // The hotel's rows before any look: who may speak with each, as it set it.
        _db.Insert(
            new PlayerChatStyleEntity
            {
                Id = 1,
                ClientStyleId = 0,
                Name = "normal",
            }
        );
        _db.Insert(
            new PlayerChatStyleEntity
            {
                Id = 2,
                ClientStyleId = 24,
                Name = "bats_old",
            }
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _host.Dispose();
        _folder.Dispose();
        _db.Dispose();
    }

    [Fact]
    public async Task a_bundle_taken_in_gives_each_style_its_look_and_the_hotel_keeps_who_may_use_it()
    {
        var result = await _styles.ImportBundleAsync(Bundle(), STAFF, Ct);

        result
            .Should()
            .Be(
                new ChatStyleImportResult
                {
                    Added = 1,
                    Updated = 2,
                    LibraryBitmaps = 1,
                }
            );

        var list = await _styles.ListAsync(Ct);

        // In the bundle's order, the client picker's.
        list.Styles.Select(x => (x.ClientStyleId, x.AssetId, x.Drawable))
            .Should()
            .Equal((24, "bats", true), (0, "normal", true), (37, "ambassador", true));
        list.LibraryBitmaps.Should().Be(1);

        // The bundle says bats is the club's; the hotel had it as everyone's, and keeps it so.
        var bats = list.Styles[0];

        bats.ClubOnly.Should().BeFalse();
        bats.Bitmaps.Should()
            .BeEquivalentTo(
                "chat_bubble_base",
                "chat_bubble_color",
                "chat_bubble_pointer",
                "selector_preview"
            );
        JsonNode.Parse(bats.RegPoints!)!["pointerY"]!.GetValue<int>().Should().Be(20);

        // A style new to the hotel takes who may use it from the bundle, and chat follows at once.
        _provider.GetChatStyle(37)!.AmbassadorOnly.Should().BeTrue();

        // A bitmap the packer trimmed is its own size again: the nine-slice counts from its edges.
        var base24 = RgbaImage.Decode(
            (await _styles.GetBitmapAsync(bats.Id, "chat_bubble_base", Ct))!
        );

        (base24.Width, base24.Height).Should().Be((40, 30));
        base24.Alpha(0, 0).Should().Be(0);
        base24.Alpha(10, 10).Should().Be(255);

        // Built again, untrimmed, with the hotel's flags, and kept as a template bundle.
        var built = BuiltBundle();
        var definitions = JsonNode.Parse(built.Files[ChatStyleService.DEFINITIONS_FILE])![
            "styles"
        ]!.AsArray();

        definitions.Select(x => x!["id"]!.GetValue<int>()).Should().Equal(24, 0, 37);
        definitions[0]!["flags"]!.AsObject().Should().NotContainKey("hcOnly");
        definitions[0]!["bitmaps"]!
            .AsArray()
            .Select(x => x!.GetValue<string>())
            .Should()
            .Equal("chat_bubble_color");
        definitions[2]!["flags"]!["ambassadorOnly"]!.GetValue<bool>().Should().BeTrue();

        var frames = JsonNode.Parse(built.Files[$"{ChatStyleService.LIBRARY}_spritesheet.json"])![
            "frames"
        ]!.AsObject();
        var frame = frames[$"{ChatStyleService.LIBRARY}-style_bats_chat_bubble_base"]!;

        frame["trimmed"]!.GetValue<bool>().Should().BeFalse();
        (frame["frame"]!["w"]!.GetValue<int>(), frame["frame"]!["h"]!.GetValue<int>())
            .Should()
            .Be((40, 30));
        frames.Should().ContainKey($"{ChatStyleService.LIBRARY}-ui_arrow");

        var row = await RowAsync();

        row.Source.Should().Be(AssetBundleSource.Built);
        row.Hash.Should().NotBeNull();
        (await _styles.ListAsync(Ct)).Bundle!.Hash.Should().Be(row.Hash);
    }

    [Fact]
    public async Task a_style_changed_is_built_into_the_bundle_and_one_it_cant_keep_changes_nothing()
    {
        await _styles.ImportBundleAsync(Bundle(), STAFF, Ct);

        var normal = (await _styles.ListAsync(Ct)).Styles.Single(x => x.ClientStyleId == 0);
        var before = BuiltBundle();

        await _styles.SaveAsync(
            normal.Id,
            new ChatStyleEdit
            {
                RegPoints = NORMAL_POINTS.Replace(
                    "\"fontSize\":12",
                    "\"fontSize\":10",
                    StringComparison.Ordinal
                ),
                StaffOverrideable = true,
                Files = new Dictionary<string, byte[]?> { ["chat_bubble_base"] = Png(50, 20, 0) },
            },
            STAFF,
            Ct
        );

        var built = BuiltBundle();
        var entry = JsonNode.Parse(built.Files[ChatStyleService.DEFINITIONS_FILE])!["styles"]!
            .AsArray()
            .Single(x => x!["id"]!.GetValue<int>() == 0)!;

        entry["regPoints"]!["fontSize"]!.GetValue<int>().Should().Be(10);
        entry["flags"]!["staffOverrideable"]!.GetValue<bool>().Should().BeTrue();
        built
            .Files[$"{ChatStyleService.LIBRARY}.png"]
            .Should()
            .NotEqual(before.Files[$"{ChatStyleService.LIBRARY}.png"]);

        async Task Refused(int id, ChatStyleEdit edit, string why)
        {
            var save = () => _styles.SaveAsync(id, edit, STAFF, Ct);

            (await save.Should().ThrowAsync<ArgumentException>())
                .Which.Message.Should()
                .Contain(why);
        }

        await Refused(
            normal.Id,
            new ChatStyleEdit { RegPoints = """{"scale9":{"x":1,"y":1,"width":1,"height":1}}""" },
            "textFieldMargins"
        );
        await Refused(
            normal.Id,
            new ChatStyleEdit
            {
                RegPoints = NORMAL_POINTS.Replace("}", ",\"wobble\":3}", StringComparison.Ordinal),
            },
            "wobble"
        );
        await Refused(
            normal.Id,
            new ChatStyleEdit
            {
                RegPoints = NORMAL_POINTS.Replace(
                    "\"pointerY\":23",
                    "\"pointerY\":\"low\"",
                    StringComparison.Ordinal
                ),
            },
            "pointerY"
        );
        await Refused(
            normal.Id,
            new ChatStyleEdit
            {
                Files = new Dictionary<string, byte[]?>
                {
                    ["chat_bubble_base"] = "not a png"u8.ToArray(),
                },
            },
            "not an image"
        );
        await Refused(
            normal.Id,
            new ChatStyleEdit
            {
                Files = new Dictionary<string, byte[]?> { ["sparkle"] = Png(2, 2, 0) },
            },
            "sparkle"
        );
        await Refused(0, new ChatStyleEdit { ClientStyleId = 50, AssetId = "bats" }, "called bats");
        await Refused(normal.Id, new ChatStyleEdit { ClubOnly = true }, "everyone's");

        BuiltBundle()
            .Files[ChatStyleService.DEFINITIONS_FILE]
            .Should()
            .Equal(built.Files[ChatStyleService.DEFINITIONS_FILE]);
    }

    [Fact]
    public async Task a_copy_starts_with_the_bitmaps_of_its_style_and_a_removed_one_leaves_the_bundle()
    {
        await _styles.ImportBundleAsync(Bundle(), STAFF, Ct);

        var bats = (await _styles.ListAsync(Ct)).Styles.Single(x => x.AssetId == "bats");
        var copy = await _styles.SaveAsync(
            0,
            new ChatStyleEdit
            {
                ClientStyleId = 60,
                AssetId = "bats_mine",
                RegPoints = bats.RegPoints,
                CopyFrom = bats.Id,
                Files = new Dictionary<string, byte[]?> { ["chat_bubble_color"] = null },
            },
            STAFF,
            Ct
        );

        var listed = (await _styles.ListAsync(Ct)).Styles.Single(x => x.Id == copy);

        listed
            .Bitmaps.Should()
            .BeEquivalentTo("chat_bubble_base", "chat_bubble_pointer", "selector_preview");
        listed.Drawable.Should().BeTrue();
        Ids(BuiltBundle()).Should().Equal(24, 0, 37, 60);

        (await _styles.DeleteAsync(copy, STAFF, Ct)).Should().BeTrue();
        Ids(BuiltBundle()).Should().Equal(24, 0, 37);
        _provider.GetChatStyle(60).Should().BeNull();
    }

    [Fact]
    public async Task a_bundle_is_taken_from_its_address_and_a_file_that_is_none_is_refused()
    {
        _host.Bundle = Bundle();

        (
            await _styles.ImportBundleFromAsync(
                "https://images.example/bundled/templates/habbo-free-flow-chat-com.nitro",
                STAFF,
                Ct
            )
        )
            .Added.Should()
            .Be(1);

        var notAddress = () => _styles.ImportBundleFromAsync("file:///etc/passwd", STAFF, Ct);
        var notBundle = () => _styles.ImportBundleAsync("hello"u8.ToArray(), STAFF, Ct);
        var otherBundle = () =>
            _styles.ImportBundleAsync(
                NitroBundle
                    .Create(new Dictionary<string, byte[]> { ["chair.json"] = "{}"u8.ToArray() })
                    .Write(),
                STAFF,
                Ct
            );

        await notAddress.Should().ThrowAsync<ArgumentException>();
        await notBundle.Should().ThrowAsync<ArgumentException>();
        (await otherBundle.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should()
            .Contain("chat-style-definitions");
    }

    private static int[] Ids(NitroBundle bundle) =>
        [
            .. JsonNode.Parse(bundle.Files[ChatStyleService.DEFINITIONS_FILE])!["styles"]!
                .AsArray()
                .Select(x => x!["id"]!.GetValue<int>()),
        ];

    private NitroBundle BuiltBundle() =>
        NitroBundle.Read(
            File.ReadAllBytes(
                _folder.Store.FullPathOf(AssetBundleKind.Template, ChatStyleService.LIBRARY)
            )
        );

    private async Task<Turbo.Database.Entities.Assets.AssetBundleEntity> RowAsync()
    {
        await using var db = _db.CreateDbContext();

        return await db.AssetBundles.SingleAsync(x => x.Kind == AssetBundleKind.Template, Ct);
    }

    /// <summary>
    /// A habbo-free-flow-chat-com bundle as nitro-studio publishes one, but packed trimmed: bats
    /// (24, the club's, with a colour layer), normal (0) and ambassador (37), and one bitmap of
    /// the library's own.
    /// </summary>
    private static byte[] Bundle()
    {
        var images = new ImageBundle();

        void Add(string name, byte[] png)
        {
            images.Images[name] = RgbaImage.Decode(png);
            images.Reference(name);
        }

        foreach (
            var (asset, extra) in new[] { ("bats", true), ("normal", false), ("ambassador", false) }
        )
        {
            // The base has a transparent border the trimming packer cuts away.
            Add($"{ChatStyleService.LIBRARY}-style_{asset}_chat_bubble_base", Png(40, 30, 5));
            Add($"{ChatStyleService.LIBRARY}-style_{asset}_chat_bubble_pointer", Png(9, 6, 0));
            Add($"{ChatStyleService.LIBRARY}-style_{asset}_selector_preview", Png(30, 20, 0));

            if (extra)
                Add($"{ChatStyleService.LIBRARY}-style_{asset}_chat_bubble_color", Png(40, 30, 0));
        }

        Add($"{ChatStyleService.LIBRARY}-ui_arrow", Png(4, 4, 0));

        var sheet = SpriteSheetPacker.Pack(images, ChatStyleService.LIBRARY)!;
        var definitions = $$"""
            {"defaultId":0,"styles":[
              {"id":24,"assetId":"bats","flags":{"hcOnly":true},"regPoints":{{NORMAL_POINTS.Replace(
                "\"pointerY\":23",
                "\"pointerY\":20",
                StringComparison.Ordinal
            )}},"bitmaps":["chat_bubble_color"]},
              {"id":0,"assetId":"normal","flags":{"staffOverrideable":true},"regPoints":{{NORMAL_POINTS}},"bitmaps":[]},
              {"id":37,"assetId":"ambassador","flags":{"ambassadorOnly":true},"regPoints":{{NORMAL_POINTS}},"bitmaps":[]}
            ]}
            """;

        return NitroBundle
            .Create(
                new Dictionary<string, byte[]>
                {
                    [$"{ChatStyleService.LIBRARY}_spritesheet.json"] = Encoding.UTF8.GetBytes(
                        sheet.Frames.ToJsonString()
                    ),
                    [$"{ChatStyleService.LIBRARY}.png"] = sheet.Sheet.EncodePng(),
                    [ChatStyleService.DEFINITIONS_FILE] = Encoding.UTF8.GetBytes(definitions),
                }
            )
            .Write();
    }

    /// <summary>An opaque image <paramref name="border"/> pixels in from a transparent edge.</summary>
    private static byte[] Png(int width, int height, int border)
    {
        var pixels = new byte[width * height * 4];

        for (var y = border; y < height - border; y++)
        for (var x = border; x < width - border; x++)
        {
            var at = ((y * width) + x) * 4;

            pixels[at] = (byte)(x * 5);
            pixels[at + 1] = (byte)(y * 7);
            pixels[at + 3] = 255;
        }

        return new RgbaImage(width, height, pixels).EncodePng();
    }

    /// <summary>The hotel's asset host, serving its published bundle.</summary>
    private sealed class Host : HttpMessageHandler, IHttpClientFactory
    {
        public byte[]? Bundle { get; set; }

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                Bundle is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(Bundle),
                    }
            );
    }
}
