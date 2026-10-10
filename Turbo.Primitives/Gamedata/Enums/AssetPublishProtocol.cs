namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>How bundles reach where the client loads them from.</summary>
public enum AssetPublishProtocol
{
    /// <summary>A folder on this server, such as the web root the asset host serves.</summary>
    Folder = 0,

    Ftp = 1,

    /// <summary>FTP over explicit TLS.</summary>
    Ftps = 2,

    Sftp = 3,
}
