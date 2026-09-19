namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// The playlist a video display was last set to, under <see cref="SECTION"/> in the item extra
/// data. Where in the playlist it is, is not kept: a display starts over when its room loads.
/// </summary>
public sealed record YoutubeDisplayData
{
    public const string SECTION = "youtube_display";

    public string PlaylistId { get; init; } = string.Empty;
}
