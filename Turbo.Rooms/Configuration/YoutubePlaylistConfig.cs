using System.Collections.Generic;

namespace Turbo.Rooms.Configuration;

/// <summary>A playlist of a video display, as <c>Turbo:Rooms:YoutubePlaylists</c> lists them.</summary>
public class YoutubePlaylistConfig
{
    /// <summary>What an item remembers having been set to; keep it stable once displays use it.</summary>
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<YoutubeVideoConfig> Videos { get; init; } = [];
}
