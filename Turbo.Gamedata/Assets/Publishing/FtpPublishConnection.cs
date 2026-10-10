using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// An FTP server, or FTPS over explicit TLS: a file is uploaded beside its place and moved over it.
/// </summary>
internal sealed class FtpPublishConnection : IPublishConnection
{
    public const int DEFAULT_PORT = 21;

    private readonly AsyncFtpClient _client;
    private readonly string _root;

    public FtpPublishConnection(
        string host,
        int port,
        string user,
        string password,
        string remotePath,
        bool secure,
        bool allowSelfSigned,
        TimeSpan timeout
    )
    {
        var milliseconds = (int)timeout.TotalMilliseconds;

        _root = remotePath;
        _client = new AsyncFtpClient(
            host,
            user,
            password,
            port == 0 ? DEFAULT_PORT : port,
            new FtpConfig
            {
                ConnectTimeout = milliseconds,
                ReadTimeout = milliseconds,
                DataConnectionConnectTimeout = milliseconds,
                DataConnectionReadTimeout = milliseconds,
                EncryptionMode = secure ? FtpEncryptionMode.Explicit : FtpEncryptionMode.None,
                ValidateAnyCertificate = secure && allowSelfSigned,
            }
        );
    }

    public string? HostKey => null;

    public Task ConnectAsync(CancellationToken ct) => _client.Connect(ct);

    public Task EnsureDirectoryAsync(string path, CancellationToken ct) =>
        _client.CreateDirectory(RemotePaths.Join(_root, path), true, ct);

    public async Task UploadAsync(Stream content, string path, CancellationToken ct)
    {
        var target = RemotePaths.Join(_root, path);
        var part = $"{target}.part";

        var status = await _client
            .UploadStream(content, part, FtpRemoteExists.Overwrite, false, null, ct)
            .ConfigureAwait(false);

        if (status != FtpStatus.Success)
            throw new IOException($"The server did not take {path}.");

        if (
            !await _client
                .MoveFile(part, target, FtpRemoteExists.Overwrite, ct)
                .ConfigureAwait(false)
        )
            throw new IOException($"The server did not put {path} in place.");
    }

    public async Task DeleteAsync(string path, CancellationToken ct)
    {
        var target = RemotePaths.Join(_root, path);

        if (await _client.FileExists(target, ct).ConfigureAwait(false))
            await _client.DeleteFile(target, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken ct) =>
        [
            .. (
                await _client.GetListing(RemotePaths.Join(_root, path), ct).ConfigureAwait(false)
            ).Select(x => x.Name),
        ];

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
