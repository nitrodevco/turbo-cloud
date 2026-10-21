using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Assets.Conversion;
using Turbo.Assets.Nitro;
using Turbo.Database.Entities.Assets;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;
using Xunit;

namespace Turbo.Tests.Assets;

/// <summary>
/// A publish draws the furniture's icons from their bundles, as nitro-studio draws them, into
/// <c>images/furni-icons</c>: the bundle's, and one for each colour the hotel's furniture has, tinted
/// as its icon visualization says. One is drawn again only when its bundle changed, goes when no
/// colour wants it, and a bundle with no icon to draw has Habbo's.
/// </summary>
public sealed class PublishedFurnitureIconsTests : IDisposable
{
    private readonly AssetPublishHotel _hotel = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _hotel.Dispose();

    [Fact]
    public async Task A_publish_sends_each_bundle_and_colour_its_icon_drawn_as_the_studio_draws_it()
    {
        await _hotel.PutBundleAsync("chair", Chair(), Ct);
        await _hotel.PutBundleAsync("lamp", Lamp(), Ct, revision: "4");
        await DefineAsync("chair", "chair*3", "lamp");
        _hotel.Habbo["https://images.habbo.com/dcr/hof_furni/4/lamp_icon.png"] = Png(
            1,
            1,
            0xFF0000FF
        );

        var target = await _hotel.AddFolderTargetAsync(Ct);
        var job = await _hotel.PublishAsync(target.Id, Ct);

        job.Log.Should().Contain(x => x.StartsWith("Furniture icons: 3 drawn for 2 bundles"));

        // Layer a (red) at -x,-y = -2,-3 under layer b (white, tinted by colour 3) at 0,0: 4x5.
        var plain = Icon("chair_icon.png");
        var green = Icon("chair_3_icon.png");

        (plain.Width, plain.Height).Should().Be((4, 5));
        Pixel(plain, 0, 0).Should().Be(0xFF0000FF);
        Pixel(plain, 2, 3).Should().Be(0xFFFFFFFF);
        Pixel(green, 2, 3).Should().Be(0x00FF00FF);
        Pixel(plain, 3, 0).Should().Be(0u, "nothing is drawn there");

        // The lamp draws none: Habbo's, at its bundle's revision.
        (
            await File.ReadAllBytesAsync(
                Path.Combine(_hotel.TargetFolder, "images", "furni-icons", "lamp_icon.png"),
                Ct
            )
        )
            .Should()
            .Equal(_hotel.Habbo["https://images.habbo.com/dcr/hof_furni/4/lamp_icon.png"]);

        // Nothing changed: nothing drawn again.
        (await _hotel.PublishAsync(target.Id, Ct))
            .Log.Should()
            .Contain(x => x.StartsWith("Furniture icons: 0 drawn"));

        // The colour gone, its icon goes; the bundle changed, its icon is drawn again.
        await UndefineAsync("chair*3");
        await _hotel.PutBundleAsync("chair", Chair(tint: 0x0000FF), Ct);

        var again = await _hotel.PublishAsync(target.Id, Ct, deleteRemoved: true);

        again
            .Log.Should()
            .Contain(x => x.StartsWith("Furniture icons: 1 drawn for 1 bundles, 1 removed"));
        File.Exists(Path.Combine(_hotel.TargetFolder, "images", "furni-icons", "chair_3_icon.png"))
            .Should()
            .BeFalse();

        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);

        (
            await dbCtx
                .AssetBundles.Where(x => x.Kind == AssetBundleKind.FurniIcon)
                .Select(x => x.Name)
                .OrderBy(x => x)
                .ToListAsync(Ct)
        )
            .Should()
            .Equal("chair_icon.png", "lamp_icon.png");
    }

    private RgbaImage Icon(string file) =>
        RgbaImage.Decode(
            File.ReadAllBytes(Path.Combine(_hotel.TargetFolder, "images", "furni-icons", file))
        );

    private static uint Pixel(RgbaImage image, int x, int y)
    {
        var at = ((y * image.Width) + x) * 4;

        return (uint)(
            (image.Pixels[at] << 24)
            | (image.Pixels[at + 1] << 16)
            | (image.Pixels[at + 2] << 8)
            | image.Pixels[at + 3]
        );
    }

    /// <summary>A 4x2 sheet: a red square, then a white one.</summary>
    private static byte[] Sheet()
    {
        var pixels = new byte[4 * 2 * 4];

        for (var y = 0; y < 2; y++)
        for (var x = 0; x < 4; x++)
        {
            var at = ((y * 4) + x) * 4;

            pixels[at] = 255;
            pixels[at + 1] = (byte)(x < 2 ? 0 : 255);
            pixels[at + 2] = (byte)(x < 2 ? 0 : 255);
            pixels[at + 3] = 255;
        }

        return new RgbaImage(4, 2, pixels).EncodePng();
    }

    /// <summary>A chair whose icon is two layers, the second tinted by colour 3.</summary>
    private static byte[] Chair(int tint = 0x00FF00) =>
        NitroBundle
            .Create(
                new Dictionary<string, byte[]>
                {
                    ["chair.json"] = Encoding.UTF8.GetBytes(
                        $$"""
                        {"type":"chair","visualizationType":"furniture_static",
                         "visualizations":[{"size":1,"angle":360,"layerCount":2,"directions":[{"id":0}],
                           "colors":[{"id":3,"layers":[{"id":1,"color":{{tint}}}]}]}],
                         "assets":[{"name":"chair_icon_a","x":2,"y":3},{"name":"chair_icon_b","x":0,"y":0}]}
                        """
                    ),
                    ["chair_spritesheet.json"] = Encoding.UTF8.GetBytes(
                        """
                        {"frames":{
                          "chair_icon_a":{"frame":{"x":0,"y":0,"w":2,"h":2},"trimmed":false},
                          "chair_icon_b":{"frame":{"x":2,"y":0,"w":2,"h":2},"trimmed":false}},
                         "meta":{"image":"chair.png"}}
                        """
                    ),
                    ["chair.png"] = Sheet(),
                }
            )
            .Write();

    /// <summary>A lamp with no icon visualization: nothing to draw its icon from.</summary>
    private static byte[] Lamp() =>
        NitroBundle
            .Create(
                new Dictionary<string, byte[]>
                {
                    ["lamp.json"] = """{"type":"lamp","visualizations":[]}"""u8.ToArray(),
                }
            )
            .Write();

    private static byte[] Png(int width, int height, uint rgba)
    {
        var pixels = new byte[width * height * 4];

        for (var at = 0; at < pixels.Length; at += 4)
        {
            pixels[at] = (byte)(rgba >> 24);
            pixels[at + 1] = (byte)(rgba >> 16);
            pixels[at + 2] = (byte)(rgba >> 8);
            pixels[at + 3] = (byte)rgba;
        }

        return new RgbaImage(width, height, pixels).EncodePng();
    }

    private async Task DefineAsync(params string[] names)
    {
        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);
        var id = 1;

        foreach (var name in names)
        {
            dbCtx.FurnitureDefinitions.Add(
                new FurnitureDefinitionEntity
                {
                    Id = id,
                    SpriteId = id++,
                    Name = name,
                    ProductType = ProductType.Floor,
                    FurniCategory = FurnitureCategory.Default,
                    Logic = "default",
                    Width = 1,
                    Length = 1,
                    StackHeight = 1,
                    CanStack = true,
                    CanWalk = false,
                    CanSit = false,
                    CanLay = false,
                    CanRecycle = false,
                    CanTrade = true,
                    CanGroup = true,
                    CanSell = true,
                }
            );
        }

        await dbCtx.SaveChangesAsync(Ct);
    }

    private async Task UndefineAsync(string name)
    {
        await using var dbCtx = await _hotel.Db.CreateDbContextAsync(Ct);

        await dbCtx.FurnitureDefinitions.Where(x => x.Name == name).ExecuteDeleteAsync(Ct);
    }
}
