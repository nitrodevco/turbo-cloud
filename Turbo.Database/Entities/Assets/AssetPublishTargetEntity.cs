using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Assets;

/// <summary>
/// Where bundles are published: a folder on this server, or an FTP, FTPS or SFTP server. The
/// password is kept sealed (AES-GCM, with the asset key) and is never sent back to the panel.
/// </summary>
[Table("asset_publish_targets")]
public class AssetPublishTargetEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 64;
    public const int HOST_MAX_LENGTH = 255;
    public const int USER_MAX_LENGTH = 128;
    public const int PATH_MAX_LENGTH = 512;

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("protocol")]
    public required AssetPublishProtocol Protocol { get; set; }

    [Column("host")]
    [MaxLength(HOST_MAX_LENGTH)]
    public string Host { get; set; } = "";

    /// <summary>Zero for the protocol's own port (21, 22).</summary>
    [Column("port")]
    public int Port { get; set; }

    [Column("user")]
    [MaxLength(USER_MAX_LENGTH)]
    public string User { get; set; } = "";

    /// <summary>The password, sealed; null when it has none.</summary>
    [Column("password")]
    public byte[]? Password { get; set; }

    /// <summary>The folder the asset host serves from, where <c>bundled/...</c> goes; a path on this server for a folder target.</summary>
    [Column("remote_path")]
    [MaxLength(PATH_MAX_LENGTH)]
    public string RemotePath { get; set; } = "";

    /// <summary>Where the client reaches what is published there (<c>https://assets.example.com</c>), for the addresses the panel shows.</summary>
    [Column("public_url")]
    [MaxLength(PATH_MAX_LENGTH)]
    public string PublicUrl { get; set; } = "";

    /// <summary>FTPS only: accept a certificate that does not check out, for a server with its own.</summary>
    [Column("allow_self_signed")]
    public bool AllowSelfSigned { get; set; }

    /// <summary>SFTP only: the server's host key fingerprint, trusted on first connection and required to match after.</summary>
    [Column("host_key")]
    [MaxLength(512)]
    public string? HostKey { get; set; }

    public IList<AssetPublishedFileEntity>? Files { get; set; }
}
