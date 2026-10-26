using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Gamedata;
using Turbo.Gamedata.ClientModules;
using Turbo.Gamedata.Configuration;
using Turbo.Gamedata.Habbo;
using Turbo.Gamedata.Plugins;
using Turbo.Gamedata.Texts;
using Turbo.Gamedata.Variables;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Settings;
using Turbo.Primitives.Texts;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The no-code path (<c>docs/plugin-data.md</c>): a theme with no assembly loads from its files, its
/// <c>theme.json</c> styles reach clients as <c>templates.styles</c>, staff's changes to them are the
/// hotel's own entries, staff's window patches live in the hotel's own data plugin, and either is
/// packaged as a data plugin another hotel installs. Inferred rules of our framework, not Habbo's.
/// </summary>
[Trait("evidence", "inferred")]
public sealed class DataPluginTests : IDisposable
{
    private const string THEME = """
        { "styles": {
            "frame.header": { "type": "color", "default": "#6b6861", "group": "Windows" },
            "frame.art": { "type": "image", "default": "frame.png", "overlay": "habbo-window-manager-com/theme-habbo-frame-frame-default" },
            "purse.width": { "type": "number", "default": 180, "min": 100, "max": 400 }
        } }
        """;

    private const string PATCHES = """
        { "patches": [ { "template": "habbo-toolbar-com/purse_xml", "order": -10, "ops": [
            { "element": "help_button", "set": { "color": "${style:frame.header}" } } ] } ] }
        """;

    private static readonly PlayerId STAFF = PlayerId.Parse(1);

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-data-plugin-" + Guid.NewGuid().ToString("N")
    );

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task A_theme_with_no_assembly_loads_and_gives_clients_its_patches_and_styles()
    {
        WriteTheme("mytheme");
        // A data plugin runs no code: its wwwroot/<key>.js is a file, not its module.
        File.WriteAllText(Path.Combine(_folder, "mytheme", "wwwroot", "mytheme.js"), "export {}");

        var hotel = Hotel();

        await hotel.Manager.LoadAllAsync(ct: Ct);

        hotel
            .Manager.Snapshot()
            .Should()
            .ContainSingle()
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
        hotel.Registry.Modules.Should().BeEmpty();
        Part(hotel, ExternalVariablesFile.TEMPLATE_PATCHES)
            .GetProperty("mytheme")
            .GetString()
            .Should()
            .StartWith("/gamedata/plugins/mytheme/template-patches.json?v=");

        var styles = Part(hotel, ExternalVariablesFile.TEMPLATE_STYLES);

        // A colour as the 0xAARRGGBB number a patch's color is; an image at its address by hash.
        styles.GetProperty("mytheme:frame.header").GetUInt32().Should().Be(0xFF6B6861);
        styles.GetProperty("mytheme:purse.width").GetDouble().Should().Be(180);
        styles
            .GetProperty("mytheme:frame.art")
            .GetString()
            .Should()
            .StartWith("/gamedata/plugins/mytheme/frame.png?v=");
        Part(hotel, ExternalVariablesFile.ASSET_OVERLAYS)
            .GetProperty("habbo-window-manager-com/theme-habbo-frame-frame-default")
            .GetString()
            .Should()
            .Be(styles.GetProperty("mytheme:frame.art").GetString());

        await hotel.Manager.UnloadAllAsync(Ct);

        hotel.Loaded.OwnedPartsOf(ExternalVariablesFile.TEMPLATE_STYLES).Should().BeEmpty();
    }

    [Fact]
    public async Task Staff_change_a_style_as_the_hotel_s_own_entry_and_reset_it_to_the_default()
    {
        WriteTheme("mytheme");

        var hotel = Hotel();
        var folder = Path.Combine(_folder, "mytheme");

        var changed = await hotel.Styles.SetAsync(
            "mytheme",
            folder,
            "frame.header",
            "#ff0000",
            STAFF,
            Ct
        );

        changed.Changed.Should().BeTrue();
        changed.Value.Should().Be("#ff0000");
        (await HotelValueAsync(hotel, ExternalVariablesFile.TEMPLATE_STYLES))
            .Should()
            .Be("""{"mytheme:frame.header":4294901760}""");

        // An image standing in for a bitmap is the hotel's overlay of it too.
        await hotel.Styles.SetAsync(
            "mytheme",
            folder,
            "frame.art",
            "/gamedata/plugins/hotel/styles/a.png",
            STAFF,
            Ct
        );
        (await HotelValueAsync(hotel, ExternalVariablesFile.ASSET_OVERLAYS))
            .Should()
            .Contain("theme-habbo-frame-frame-default");

        var refused = () =>
            hotel.Styles.SetAsync("mytheme", folder, "purse.width", "900", STAFF, Ct);
        await refused.Should().ThrowAsync<ArgumentException>().WithMessage("*from 100 to 400*");
        var notColour = () =>
            hotel.Styles.SetAsync("mytheme", folder, "frame.header", "red", STAFF, Ct);
        await notColour.Should().ThrowAsync<ArgumentException>().WithMessage("*colour*");

        (await hotel.Styles.ListAsync("mytheme", folder, Ct))
            .Where(x => x.Changed)
            .Select(x => x.Property.Name)
            .Should()
            .BeEquivalentTo("frame.header", "frame.art");

        var reset = await hotel.Styles.ResetAsync("mytheme", folder, "frame.header", STAFF, Ct);
        await hotel.Styles.ResetAsync("mytheme", folder, "frame.art", STAFF, Ct);

        reset!.Changed.Should().BeFalse();
        reset.Value.Should().Be("#6b6861");
        (await HotelValueAsync(hotel, ExternalVariablesFile.TEMPLATE_STYLES)).Should().BeNull();
        (await HotelValueAsync(hotel, ExternalVariablesFile.ASSET_OVERLAYS)).Should().BeNull();
    }

    [Fact]
    public async Task Staff_patches_live_in_the_hotel_s_data_plugin_and_a_disabled_one_is_not_sent()
    {
        var hotel = Hotel();
        var patches = hotel.HotelPlugin;

        var bad = () =>
            patches.SavePatchAsync(
                Patch(
                    "Bad",
                    """[ { "element": "help_button", "set": { "colour": 1 }, "remove": true } ]"""
                ),
                Ct
            );
        await bad.Should()
            .ThrowAsync<ArgumentException>()
            .WithMessage("*one of set, remove, insert, move, wrap or bind*");

        var green = await patches.SavePatchAsync(
            Patch(
                "Green help",
                """[ { "element": "help_button", "set": { "color": 4281244476 } } ]"""
            ),
            Ct
        );
        await patches.SavePatchAsync(
            Patch("Hidden beta", """[ { "element": "beta_sign", "remove": true } ]""") with
            {
                Disabled = true,
            },
            Ct
        );

        hotel
            .Manager.Snapshot()
            .Should()
            .ContainSingle(x => x.Key == IHotelDataPlugin.KEY)
            .Which.State.Should()
            .Be(PluginStateType.Loaded);
        Part(hotel, ExternalVariablesFile.TEMPLATE_PATCHES)
            .GetProperty(IHotelDataPlugin.KEY)
            .GetString()
            .Should()
            .Contain("template-patches.json?v=");

        var served = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(_folder, IHotelDataPlugin.KEY, "wwwroot", "template-patches.json")
            )
        );

        served
            .RootElement.GetProperty("patches")
            .GetArrayLength()
            .Should()
            .Be(1, "the disabled patch is kept, not sent");
        (await patches.PatchesAsync(Ct))
            .Select(x => x.Name)
            .Should()
            .Equal("Green help", "Hidden beta");

        (await patches.RemovePatchAsync(green.Id, Ct)).Should().BeTrue();
        (await patches.PatchesAsync(Ct)).Should().ContainSingle();
    }

    /// <summary>
    /// The server writes the hotel's own plugin's manifest with the plugin API it has, so one written
    /// under another API (every hotel's, 4 to 9, when the API went back to 1) is stamped with this
    /// server's and loads with staff's patches; another data plugin built for another API is still
    /// refused.
    /// </summary>
    [Fact]
    public async Task The_hotel_s_own_plugin_written_under_another_plugin_api_loads_with_its_patches()
    {
        var own = Path.Combine(_folder, IHotelDataPlugin.KEY);

        Directory.CreateDirectory(Path.Combine(own, "wwwroot"));
        File.WriteAllText(
            Path.Combine(own, "manifest.json"),
            """{ "Name": "Hotel changes", "Key": "hotel", "Version": "1.0.0", "Author": "Staff", "ApiVersion": 9 }"""
        );
        File.WriteAllText(
            Path.Combine(own, "wwwroot", PluginGamedataSeeds.TEMPLATE_PATCHES_FILE),
            PATCHES
        );
        WriteTheme("oldtheme");
        File.WriteAllText(
            Path.Combine(_folder, "oldtheme", "manifest.json"),
            """{ "Name": "Old theme", "Key": "oldtheme", "Version": "1.0.0", "Author": "a", "ApiVersion": 9 }"""
        );

        var hotel = Hotel();

        await hotel.Manager.LoadAllAsync(ct: Ct);

        var states = hotel.Manager.Snapshot().ToDictionary(x => x.Key, x => x.State);

        states[IHotelDataPlugin.KEY].Should().Be(PluginStateType.Loaded);
        states["oldtheme"].Should().Be(PluginStateType.Failed);
        Part(hotel, ExternalVariablesFile.TEMPLATE_PATCHES)
            .GetProperty(IHotelDataPlugin.KEY)
            .GetString()
            .Should()
            .Contain("template-patches.json?v=");
        PluginHelpers.ReadManifest(own).ApiVersion.Should().Be(PluginApi.VERSION);
    }

    /// <summary>
    /// One window address (nitro-next docs/template-patches.md): a root patch's position restyles the
    /// window's frame, and a window drawn without a template is named by its frame id - both saved
    /// from the panel; a position that is not two numbers is refused.
    /// </summary>
    [Fact]
    public async Task Staff_patch_a_window_s_frame_by_its_root_position_and_a_frame_by_its_id()
    {
        var patches = Hotel().HotelPlugin;

        await patches.SavePatchAsync(
            Patch(
                "Catalogue",
                """[ { "element": "", "set": { "color": 4288688176, "position": { "x": 84, "y": 153 } } } ]"""
            ) with
            {
                Template = "habbo-catalog-com/catalog_ubuntu_with_tabs",
            },
            Ct
        );
        await patches.SavePatchAsync(
            Patch("Wired", """[ { "element": "", "set": { "color": 4282019493 } } ]""") with
            {
                Template = "wired-setup",
            },
            Ct
        );

        var bad = () =>
            patches.SavePatchAsync(
                Patch("Bad", """[ { "element": "", "set": { "position": [ 84, 153 ] } } ]"""),
                Ct
            );
        await bad.Should().ThrowAsync<ArgumentException>().WithMessage("*first opens*");

        (await patches.PatchesAsync(Ct))
            .Select(x => x.Template)
            .Should()
            .Equal("habbo-catalog-com/catalog_ubuntu_with_tabs", "wired-setup");
    }

    // The panel's New patch saves what the client binds (nitro-next templatePatchBind.ts,
    // templateBindProblems): S2 could draw the catalogue's tab icons only from a module, so a hotel
    // could not at all. The check refuses what no client fills, before it is saved.
    [Fact]
    public async Task A_staff_bind_op_is_saved_and_one_naming_what_no_client_fills_is_refused()
    {
        var patches = Hotel().HotelPlugin;

        var refused = () =>
            patches.SavePatchAsync(
                Patch(
                    "Tab icons",
                    """
                    [ { "element": "tab_button/icon", "bind": { "asset": "icons/${page.icon}.png", "tooltip": "${item.pageName}", "colour": 1 } } ]
                    """
                ),
                Ct
            );
        (await refused.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should()
            .Contain("${page.icon}: a placeholder is")
            .And.Contain("${item.pageName}, which a row has")
            .And.Contain("colour, which is no binding field");

        await patches.SavePatchAsync(
            Patch(
                "Tab icons",
                """
                [
                  { "element": "tab_button", "insert": "last", "add": { "tag": "bitmap", "name": "icon", "width": 20, "height": 20 } },
                  { "element": "tab_button/icon", "bind": { "asset": "icons/tab${item.index}.png", "tooltip": "${text.catalog.title}: ${item.pageName}", "selected": "${ctx.selected}" }, "items": true }
                ]
                """
            ),
            Ct
        );

        var served = JsonDocument.Parse(
            File.ReadAllText(
                Path.Combine(_folder, IHotelDataPlugin.KEY, "wwwroot", "template-patches.json")
            )
        );

        served
            .RootElement.GetProperty("patches")[0]
            .GetProperty("ops")[1]
            .GetProperty("bind")
            .GetProperty("asset")
            .GetString()
            .Should()
            .Be("icons/tab${item.index}.png", "the client fills it per tab");
    }

    // The client wraps since r58 (nitro-next templatePatch.ts, `wrap`): the panel refused it as no op.
    // The patch is the documentation's own example (nitro-next docs/template-patches.md, Operations).
    [Fact]
    public async Task A_staff_wrap_op_is_saved_and_the_window_itself_is_not_wrapped()
    {
        var patches = Hotel().HotelPlugin;

        var refused = () =>
            patches.SavePatchAsync(
                Patch(
                    "Panes",
                    """
                    [ { "element": "", "wrap": { "tag": "frame" } },
                      { "element": "navigationContainer", "wrap": { "caption": "Shop" } } ]
                    """
                ),
                Ct
            );
        (await refused.Should().ThrowAsync<ArgumentException>())
            .Which.Message.Should()
            .Contain("Op 1 wraps the window itself")
            .And.Contain(
                "Op 2 adds an element \"like\" one the template has, or gives its \"tag\""
            );

        var saved = await patches.SavePatchAsync(
            Patch(
                "Panes",
                """
                [ { "element": "navigationContainer", "wrap": { "tag": "frame", "name": "nav", "style": "0", "caption": "Shop", "margins": [ 6, 28, 6, 6 ] } } ]
                """
            ),
            Ct
        );

        saved.Ops[0].GetProperty("wrap").GetProperty("name").GetString().Should().Be("nav");
    }

    [Fact]
    public async Task A_theme_with_the_hotel_s_styles_is_packaged_as_a_data_plugin_that_installs_elsewhere()
    {
        WriteTheme("mytheme");

        var hotel = Hotel();

        await hotel.Manager.LoadAllAsync(ct: Ct);

        var upload = await hotel.HotelPlugin.SaveFileAsync(
            "styles/mytheme/frame-art.png",
            new MemoryStream([1, 2, 3]),
            Ct
        );

        upload.Should().StartWith("/gamedata/plugins/hotel/styles/mytheme/frame-art.png?v=");
        await hotel.Styles.SetAsync(
            "mytheme",
            Path.Combine(_folder, "mytheme"),
            "frame.header",
            "#123456",
            STAFF,
            Ct
        );
        await hotel.Styles.SetAsync(
            "mytheme",
            Path.Combine(_folder, "mytheme"),
            "frame.art",
            upload,
            STAFF,
            Ct
        );

        var zip = await hotel.HotelPlugin.ExportAsync(
            "mytheme",
            new PluginExportRequest("ourtheme", "Our theme", "1.0.0", null),
            Ct
        );

        using var archive = new ZipArchive(new MemoryStream(zip!));
        var names = archive.Entries.Select(x => x.FullName).ToList();

        names
            .Should()
            .Contain([
                "manifest.json",
                "theme.json",
                "wwwroot/template-patches.json",
                "wwwroot/styles/frame-art.png",
            ]);

        var theme = JsonDocument
            .Parse(archive.GetEntry("theme.json")!.Open())
            .RootElement.GetProperty("styles");

        theme.GetProperty("frame.header").GetProperty("default").GetString().Should().Be("#123456");
        theme
            .GetProperty("frame.art")
            .GetProperty("default")
            .GetString()
            .Should()
            .Be("styles/frame-art.png");
        JsonDocument
            .Parse(archive.GetEntry("manifest.json")!.Open())
            .RootElement.TryGetProperty("AssemblyFile", out _)
            .Should()
            .BeFalse();

        // Another hotel installs it as it installs any package.
        var other = Path.Combine(_folder, "other");
        var install = new PluginAdministration(
            new PluginManager(
                new ServiceCollection().BuildServiceProvider(),
                new AssemblyProcessor([]),
                Options.Create(new PluginConfig { PluginFolderPath = other, UploadEnabled = true }),
                NullLogger<PluginManager>.Instance
            ),
            new Turbo.Plugins.Data.PluginTables(
                _db,
                NullLogger<Turbo.Plugins.Data.PluginTables>.Instance
            ),
            [],
            NullLogger<PluginAdministration>.Instance
        );

        var result = await install.InstallAsync(new MemoryStream(zip!), Ct);

        result.Ok.Should().BeTrue(result.Message);
        result.Plugin!.State.Should().Be(PluginStateType.Loaded);

        var refused = () =>
            hotel.HotelPlugin.ExportAsync(
                "mytheme",
                new PluginExportRequest(IHotelDataPlugin.KEY, "x", "1", null),
                Ct
            );
        await refused.Should().ThrowAsync<ArgumentException>();
    }

    /// <summary>
    /// The base theme (docs/plugin-data.md): the hotel's own plugin, with no theme.json, lists
    /// Habbo's style properties; with no change nothing reaches clients, and a change is the hotel's
    /// entry of the variable the property names, field by field under a theme's own entry there.
    /// </summary>
    [Fact]
    public async Task The_hotel_s_plugin_extends_the_base_theme_and_a_change_is_the_hotel_s_entry_of_its_variable()
    {
        var hotel = Hotel();
        var folder = hotel.HotelPlugin.FolderOf(IHotelDataPlugin.KEY)!;

        var listed = await hotel.Styles.ListAsync(IHotelDataPlugin.KEY, folder, Ct);

        listed
            .Select(x => x.Property.Name)
            .Should()
            .Contain(["catalog.color", "catalog.position", "window.frame", "loading.logo"]);
        listed.Should().OnlyContain(x => !x.Changed && x.Value == null);

        // Loaded with no change, it gives clients nothing: they draw exactly as without it.
        await hotel.HotelPlugin.SavePatchAsync(
            Patch("Hidden beta", """[ { "element": "beta_sign", "remove": true } ]"""),
            Ct
        );
        foreach (
            var variable in ExternalVariablesFile.MERGED.Except([
                ExternalVariablesFile.TEMPLATE_PATCHES,
            ])
        )
            hotel.Loaded.OwnedPartsOf(variable).Should().BeEmpty(variable);

        await hotel.Styles.SetAsync(
            IHotelDataPlugin.KEY,
            folder,
            "catalog.color",
            "#b0452c",
            STAFF,
            Ct
        );
        await hotel.Styles.SetAsync(
            IHotelDataPlugin.KEY,
            folder,
            "catalog.position",
            "84, 153",
            STAFF,
            Ct
        );

        (await HotelValueAsync(hotel, ExternalVariablesFile.FRAME_OVERRIDES))
            .Should()
            .Be("""{"catalog":{"color":"#b0452c","position":{"x":84,"y":153}}}""");

        // A theme's own entry for the window keeps what staff did not change.
        ExternalVariablesFile
            .Merge(
                ["""{"catalog":{"variant":"3","position":{"x":1,"y":2}}}"""],
                await HotelValueAsync(hotel, ExternalVariablesFile.FRAME_OVERRIDES)
            )
            .Should()
            .Be("""{"catalog":{"variant":"3","position":{"x":84,"y":153},"color":"#b0452c"}}""");

        var notPosition = () =>
            hotel.Styles.SetAsync(
                IHotelDataPlugin.KEY,
                folder,
                "catalog.position",
                "84",
                STAFF,
                Ct
            );
        await notPosition.Should().ThrowAsync<ArgumentException>().WithMessage("*x,y*");

        await hotel.Styles.ResetAsync(IHotelDataPlugin.KEY, folder, "catalog.color", STAFF, Ct);
        await hotel.Styles.ResetAsync(IHotelDataPlugin.KEY, folder, "catalog.position", STAFF, Ct);

        (await HotelValueAsync(hotel, ExternalVariablesFile.FRAME_OVERRIDES)).Should().BeNull();
        (await HotelValueAsync(hotel, ExternalVariablesFile.TEMPLATE_STYLES)).Should().BeNull();
    }

    /// <summary>
    /// <c>"extends"</c>: a theme gets every property of the one it names, a redeclaration with only a
    /// default keeps the parent's type and target, and the base theme's own (no default) give clients
    /// nothing; a theme extending another plugin inherits through it; a circle is refused.
    /// </summary>
    [Fact]
    public async Task A_theme_that_extends_the_base_theme_gives_only_the_defaults_it_redeclares()
    {
        WriteFile(
            "child",
            ThemeStyles.FILE,
            """
            { "extends": "habbo", "styles": {
                "catalog.color": { "default": "#7a3fa0" },
                "purse.frame": { "default": "purse-border.png" },
                "shop.name": { "type": "text", "default": "Bazaar" }
            } }
            """
        );
        File.WriteAllBytes(
            Path.Combine(_folder, "child", "wwwroot", "purse-border.png"),
            [137, 80, 78, 71]
        );
        // Named to load before the theme it extends: folders are listed in no set order (on Linux not by name).
        WriteFile("achild", ThemeStyles.FILE, """{ "extends": "child" }""");

        var hotel = Hotel();

        await hotel.Manager.LoadAllAsync(ct: Ct);

        var child = await hotel.Styles.ListAsync("child", Path.Combine(_folder, "child"), Ct);
        var color = child.Single(x => x.Property.Name == "catalog.color");

        color.Property.Type.Should().Be(ThemeStyleType.Color);
        color.Property.Group.Should().Be("Catalogue");
        color.Value.Should().Be("#7a3fa0");
        child.Last().Property.Name.Should().Be("shop.name");
        (await hotel.Styles.ListAsync("achild", Path.Combine(_folder, "achild"), Ct))
            .Select(x => x.Property.Name)
            .Should()
            .Equal(child.Select(x => x.Property.Name));

        // The theme extending the child inherits the child's default with the rest, though it loaded first.
        hotel
            .Loaded.OwnedPartsOf(ExternalVariablesFile.FRAME_OVERRIDES)
            .Should()
            .Equal(
                ("achild", """{"catalog":{"color":"#7a3fa0"}}"""),
                ("child", """{"catalog":{"color":"#7a3fa0"}}""")
            );
        JsonDocument
            .Parse(hotel.Loaded.OwnedPartsOf(ExternalVariablesFile.ASSET_OVERLAYS)[0].Part)
            .RootElement.EnumerateObject()
            .Should()
            .ContainSingle()
            .Which.Name.Should()
            .Be("habbo-window-manager-com/theme-habbo-border-9-border-9-default");

        WriteFile("child", ThemeStyles.FILE, """{ "extends": "achild" }""");
        var circle = () => hotel.Styles.ListAsync("child", Path.Combine(_folder, "child"), Ct);
        await circle.Should().ThrowAsync<ArgumentException>().WithMessage("*circle*");
    }

    /// <summary>
    /// The Theme tab's Images: an upload is kept in the plugin's <c>wwwroot/styles/</c>, listed with
    /// the path a patch names and the address a variable names; content that is not the image its
    /// name says is refused.
    /// </summary>
    [Fact]
    public async Task An_image_uploaded_for_a_plugin_is_listed_with_its_path_and_address()
    {
        var hotel = Hotel();

        (await hotel.HotelPlugin.ImagesAsync(IHotelDataPlugin.KEY, Ct))!.Value.Should().BeEmpty();

        var saved = await hotel.HotelPlugin.SaveImageAsync(
            IHotelDataPlugin.KEY,
            "catalog-frame.png",
            new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]),
            Ct
        );

        saved.Path.Should().Be("styles/catalog-frame.png");
        saved.Url.Should().StartWith("/gamedata/plugins/hotel/styles/catalog-frame.png?v=");
        (await hotel.HotelPlugin.ImagesAsync(IHotelDataPlugin.KEY, Ct))!
            .Value.Should()
            .ContainSingle()
            .Which.Path.Should()
            .Be("styles/catalog-frame.png");

        var notPng = () =>
            hotel.HotelPlugin.SaveImageAsync(
                IHotelDataPlugin.KEY,
                "x.png",
                new MemoryStream("<svg/>"u8.ToArray()),
                Ct
            );
        await notPng.Should().ThrowAsync<ArgumentException>().WithMessage("*not a PNG*");
        var outside = () =>
            hotel.HotelPlugin.SaveImageAsync(
                IHotelDataPlugin.KEY,
                "x.svg",
                new MemoryStream([1]),
                Ct
            );
        await outside.Should().ThrowAsync<ArgumentException>();

        (await hotel.HotelPlugin.RemoveImageAsync(IHotelDataPlugin.KEY, "../manifest.json", Ct))
            .Should()
            .BeFalse();
        (await hotel.HotelPlugin.RemoveImageAsync(IHotelDataPlugin.KEY, saved.Path, Ct))
            .Should()
            .BeTrue();
        (await hotel.HotelPlugin.ImagesAsync(IHotelDataPlugin.KEY, Ct))!.Value.Should().BeEmpty();
    }

    /// <summary>Make a plugin of the hotel's own: its base theme changes become a theme.json that extends it.</summary>
    [Fact]
    public async Task The_hotel_s_base_theme_changes_are_packaged_as_a_theme_that_extends_it()
    {
        var hotel = Hotel();
        var folder = hotel.HotelPlugin.FolderOf(IHotelDataPlugin.KEY)!;

        await hotel.HotelPlugin.SavePatchAsync(
            Patch("Hidden beta", """[ { "element": "beta_sign", "remove": true } ]"""),
            Ct
        );
        await hotel.Styles.SetAsync(
            IHotelDataPlugin.KEY,
            folder,
            "navigator.color",
            "#3a6ea5",
            STAFF,
            Ct
        );

        var zip = await hotel.HotelPlugin.ExportAsync(
            IHotelDataPlugin.KEY,
            new PluginExportRequest("ourlook", "Our look", "1.0.0", null),
            Ct
        );

        using var archive = new ZipArchive(new MemoryStream(zip!));
        var theme = JsonDocument.Parse(archive.GetEntry(ThemeStyles.FILE)!.Open()).RootElement;

        theme.GetProperty("extends").GetString().Should().Be("habbo");
        theme
            .GetProperty("styles")
            .EnumerateObject()
            .Should()
            .ContainSingle()
            .Which.Value.EnumerateObject()
            .Select(x => (x.Name, x.Value.GetString()))
            .Should()
            .Equal(("default", "#3a6ea5"));
    }

    private void WriteFile(string key, string name, string content)
    {
        var folder = Path.Combine(_folder, key);

        Directory.CreateDirectory(Path.Combine(folder, "wwwroot"));
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            $$"""{ "Name": "{{key}}", "Key": "{{key}}", "Version": "1.0.0", "Author": "a" }"""
        );
        File.WriteAllText(Path.Combine(folder, name), content);
    }

    [Fact]
    public async Task A_plugin_s_phrases_list_with_its_default_beside_staff_s_change()
    {
        WriteTheme("mytheme");
        Directory.CreateDirectory(Path.Combine(_folder, "mytheme", PluginGamedataSeeds.FOLDER));
        File.WriteAllText(
            Path.Combine(
                _folder,
                "mytheme",
                PluginGamedataSeeds.FOLDER,
                PluginGamedataSeeds.TEXTS_FILE
            ),
            "mytheme.title=My theme\nmytheme.hint=Pick a colour\n"
        );

        var hotel = Hotel();

        await hotel.Manager.LoadAllAsync(ct: Ct);
        await hotel.Texts.SaveAsync("mytheme.title", "Our theme", STAFF, Ct);
        await hotel.Texts.SaveAsync("other.text", "Not the plugin's", STAFF, Ct);

        var phrases = await hotel.Texts.SearchAsync(null, 0, Ct, "mytheme");

        phrases
            .Items.Select(x => (x.Key, x.Value, x.Plugin, x.PluginValue))
            .Should()
            .Equal(
                ("mytheme.hint", "Pick a colour", "mytheme", "Pick a colour"),
                ("mytheme.title", "Our theme", "mytheme", "My theme")
            );
        (await hotel.Texts.SearchAsync("other", 0, Ct))
            .Items.Should()
            .ContainSingle()
            .Which.Plugin.Should()
            .BeNull();
    }

    private static HotelPatch Patch(string name, string ops) =>
        new(
            string.Empty,
            name,
            "habbo-toolbar-com/purse_xml",
            null,
            JsonDocument.Parse(ops).RootElement,
            false
        );

    private void WriteTheme(string key)
    {
        var folder = Path.Combine(_folder, key);

        Directory.CreateDirectory(Path.Combine(folder, "wwwroot"));
        File.WriteAllText(
            Path.Combine(folder, "manifest.json"),
            $$"""{ "Name": "My theme", "Key": "{{key}}", "Version": "1.0.0", "Author": "a" }"""
        );
        File.WriteAllText(Path.Combine(folder, ThemeStyles.FILE), THEME);
        File.WriteAllText(
            Path.Combine(folder, "wwwroot", PluginGamedataSeeds.TEMPLATE_PATCHES_FILE),
            PATCHES
        );
        File.WriteAllBytes(Path.Combine(folder, "wwwroot", "frame.png"), [137, 80, 78, 71]);
    }

    private static JsonElement Part(HotelUnderTest hotel, string variable) =>
        JsonDocument
            .Parse(hotel.Loaded.OwnedPartsOf(variable).Should().ContainSingle().Which.Part)
            .RootElement;

    private static async Task<string?> HotelValueAsync(HotelUnderTest hotel, string key) =>
        (await hotel.Variables.GetValuesAsync([key], Ct)).GetValueOrDefault(key);

    private sealed record HotelUnderTest(
        PluginManager Manager,
        LoadedPluginGamedata Loaded,
        ClientModuleRegistry Registry,
        GamedataVariableService Variables,
        GamedataTextService Texts,
        IPluginStyles Styles,
        HotelDataPlugin HotelPlugin
    );

    /// <summary>A hotel's plugin manager over the test's plugins folder, with the gamedata services data plugins reach.</summary>
    private HotelUnderTest Hotel()
    {
        var loaded = new LoadedPluginGamedata();
        var config = Options.Create(new GamedataConfig());
        var files = _fakes.Create<IGamedataFileService>();
        var writes = new GamedataWriteLock();
        var variables = new GamedataVariableService(
            _db,
            config,
            files,
            writes,
            _fakes.Create<IServerSettings>(),
            new HabboGamedataClient(
                _fakes.Create<IHttpClientFactory>(),
                NullLogger<HabboGamedataClient>.Instance
            ),
            NullLogger<GamedataVariableService>.Instance
        );
        var texts = new GamedataTextService(
            _db,
            config,
            files,
            writes,
            TimeProvider.System,
            _fakes.Create<IHotelTextProvider>(),
            NullLogger<GamedataTextService>.Instance
        );
        var registry = new ClientModuleRegistry(
            new TestOptionsMonitor<GamedataConfig>(new GamedataConfig()),
            NullLogger<ClientModuleRegistry>.Instance
        );
        HotelDataPlugin? hotelPlugin = null;
        var host = new ServiceCollection()
            .AddSingleton<IClientModuleDirectory>(registry)
            .AddSingleton<IPluginStyles>(sp => new PluginStyleService(variables, sp))
            .AddSingleton<IHotelDataPlugin>(_ => hotelPlugin!)
            .AddSingleton<Turbo.Primitives.Networking.Extensions.IExtensionPacketRegistry>(
                new Turbo.Networking.Extensions.ExtensionPacketRegistry()
            )
            .AddSingleton<IPluginGamedataSeeds>(sp => new PluginGamedataSeeds(
                _db,
                variables,
                texts,
                files,
                loaded,
                registry,
                NullLogger<PluginGamedataSeeds>.Instance,
                sp
            ))
            .BuildServiceProvider();
        var manager = new PluginManager(
            host,
            new AssemblyProcessor([]),
            Options.Create(new PluginConfig { PluginFolderPath = _folder }),
            NullLogger<PluginManager>.Instance
        );

        hotelPlugin = new HotelDataPlugin(manager, host);

        return new HotelUnderTest(
            manager,
            loaded,
            registry,
            variables,
            texts,
            host.GetRequiredService<IPluginStyles>(),
            hotelPlugin
        );
    }
}
