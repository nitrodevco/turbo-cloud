namespace Turbo.Rooms.Configuration;

/// <summary>One video of a <see cref="YoutubePlaylistConfig"/>.</summary>
public class YoutubeVideoConfig
{
    /// <summary>The YouTube video id, the part after <c>v=</c>.</summary>
    public string VideoId { get; init; } = string.Empty;

    /// <summary>When the display moves on to the next video.</summary>
    public int DurationSeconds { get; init; }
}
