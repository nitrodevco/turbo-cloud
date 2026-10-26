using Orleans;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A file in a bundle (an image, the asset data) that differs from the target's copy to the hotel's,
/// with its size in bytes on each side, and its pixel size on each side when it is a PNG or a GIF.
/// For a file that is not a bundle (a badge, an icon), the file itself, named as it is.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AssetEntryDiff(
    [property: Id(0)] string Name,
    [property: Id(1)] AssetDiffChange Change,
    [property: Id(2)] long? BytesBefore,
    [property: Id(3)] long? BytesAfter,
    [property: Id(4)] AssetPixelSize? PixelsBefore,
    [property: Id(5)] AssetPixelSize? PixelsAfter
);

/// <summary>An image's width and height in pixels.</summary>
[GenerateSerializer, Immutable]
public sealed record AssetPixelSize([property: Id(0)] int Width, [property: Id(1)] int Height);
