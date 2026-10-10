using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// One connection to a publish target. Paths are relative to the target's remote path, with
/// forward slashes (<c>bundled/furniture</c>); an empty path is the remote path itself.
/// </summary>
internal interface IPublishConnection : IAsyncDisposable
{
    /// <summary>SFTP: the fingerprint of the host key the server showed, once connected.</summary>
    string? HostKey { get; }

    Task ConnectAsync(CancellationToken ct);

    /// <summary>Makes the folder and those above it, when they are missing.</summary>
    Task EnsureDirectoryAsync(string path, CancellationToken ct);

    /// <summary>Uploads to <c>&lt;path&gt;.part</c> and renames that over <paramref name="path"/>.</summary>
    Task UploadAsync(Stream content, string path, CancellationToken ct);

    /// <summary>Deletes the file, when there is one.</summary>
    Task DeleteAsync(string path, CancellationToken ct);

    /// <summary>The names in the folder.</summary>
    Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken ct);
}
