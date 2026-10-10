using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A publish target as the panel shows it: where it is, whether it has a password (never the
/// password), how many bundles it lacks or holds an older copy of, and its newest publish.
/// </summary>
public sealed record AssetPublishTargetSnapshot
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required AssetPublishProtocol Protocol { get; init; }

    public required string Host { get; init; }

    /// <summary>Zero for the protocol's own port.</summary>
    public required int Port { get; init; }

    public required string User { get; init; }

    public required bool HasPassword { get; init; }

    public required string RemotePath { get; init; }

    public required string PublicUrl { get; init; }

    public required bool AllowSelfSigned { get; init; }

    /// <summary>SFTP: the host key fingerprint trusted, once it has connected.</summary>
    public string? HostKey { get; init; }

    /// <summary>Bundles it lacks or holds an older copy of.</summary>
    public required int Pending { get; init; }

    public AssetPublishSnapshot? LastPublish { get; init; }
}
