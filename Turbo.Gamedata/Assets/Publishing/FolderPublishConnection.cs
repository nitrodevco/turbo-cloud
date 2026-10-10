using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>A folder on this server: a file is copied beside its place and moved over it.</summary>
internal sealed class FolderPublishConnection(string remotePath) : IPublishConnection
{
    private readonly string _root = Path.GetFullPath(remotePath);

    public string? HostKey => null;

    public Task ConnectAsync(CancellationToken ct) =>
        Directory.Exists(_root)
            ? Task.CompletedTask
            : throw new DirectoryNotFoundException($"The folder {_root} doesn't exist.");

    public Task EnsureDirectoryAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(FullPathOf(path));

        return Task.CompletedTask;
    }

    public async Task UploadAsync(Stream content, string path, CancellationToken ct)
    {
        var target = FullPathOf(path);
        var part = $"{target}.part";

        try
        {
            var file = new FileStream(
                part,
                new FileStreamOptions
                {
                    Mode = FileMode.Create,
                    Access = FileAccess.Write,
                    Options = FileOptions.Asynchronous,
                }
            );

            await using (file.ConfigureAwait(false))
                await content.CopyToAsync(file, ct).ConfigureAwait(false);

            File.Move(part, target, overwrite: true);
        }
        finally
        {
            if (File.Exists(part))
                File.Delete(part);
        }
    }

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var target = FullPathOf(path);

        if (File.Exists(target))
            File.Delete(target);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([
            .. Directory.EnumerateFileSystemEntries(FullPathOf(path)).Select(Path.GetFileName)!,
        ]);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>The path on this server, which must stay under the target's folder.</summary>
    private string FullPathOf(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));

        return
            full == _root
            || full.StartsWith(
                _root.EndsWith(Path.DirectorySeparatorChar)
                    ? _root
                    : _root + Path.DirectorySeparatorChar,
                StringComparison.Ordinal
            )
            ? full
            : throw new ArgumentException($"{path} is outside the target's folder.", nameof(path));
    }
}
