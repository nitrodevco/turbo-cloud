using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A publish target as staff enter it. <see cref="Password"/> null keeps the saved one (none, for
/// a new target), and empty removes it; <see cref="CloudflareToken"/> likewise.
/// </summary>
public sealed record AssetPublishTargetEdit
{
    public required string Name { get; init; }

    public required AssetPublishProtocol Protocol { get; init; }

    public string Host { get; init; } = "";

    /// <summary>Zero for the protocol's own port (21, 22).</summary>
    public int Port { get; init; }

    public string User { get; init; } = "";

    public string? Password { get; init; }

    public string RemotePath { get; init; } = "";

    public string PublicUrl { get; init; } = "";

    public bool AllowSelfSigned { get; init; }

    /// <summary>The Cloudflare zone the public URL is served through; empty: no purge after a publish.</summary>
    public string CloudflareZoneId { get; init; } = "";

    /// <summary>An API token allowed to purge that zone's cache.</summary>
    public string? CloudflareToken { get; init; }
}
