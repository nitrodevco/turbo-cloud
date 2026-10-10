using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// An SFTP server, signed in to with a password. Its host key is trusted the first time
/// (when none is trusted yet) and must match after: a different key refuses to
/// connect. A file is uploaded beside its place and renamed over it.
/// </summary>
internal sealed class SftpPublishConnection : IPublishConnection
{
    public const int DEFAULT_PORT = 22;
    public const string FINGERPRINT_PREFIX = "SHA256:";

    private readonly SftpClient _client;
    private readonly string _root;
    private readonly string? _trustedHostKey;
    private readonly ILogger _logger;

    public SftpPublishConnection(
        string host,
        int port,
        string user,
        string password,
        string remotePath,
        string? trustedHostKey,
        TimeSpan timeout,
        ILogger logger
    )
    {
        _root = remotePath;
        _trustedHostKey = trustedHostKey;
        _logger = logger;
        _client = new SftpClient(
            new PasswordConnectionInfo(host, port == 0 ? DEFAULT_PORT : port, user, password)
            {
                Timeout = timeout,
            }
        );
        _client.HostKeyReceived += OnHostKeyReceived;
    }

    public string? HostKey { get; private set; }

    public async Task ConnectAsync(CancellationToken ct)
    {
        try
        {
            await _client.ConnectAsync(ct).ConfigureAwait(false);
        }
        catch (SshConnectionException ex)
            when (_trustedHostKey is not null && HostKey is not null && HostKey != _trustedHostKey)
        {
            throw new InvalidOperationException(
                $"The server's host key is {HostKey}, not the {_trustedHostKey} trusted before. If the server's key really changed, forget the old one and connect again.",
                ex
            );
        }
    }

    public async Task EnsureDirectoryAsync(string path, CancellationToken ct)
    {
        foreach (var folder in RemotePaths.Ancestry(RemotePaths.Join(_root, path)))
        {
            if (!await _client.ExistsAsync(folder, ct).ConfigureAwait(false))
                await _client.CreateDirectoryAsync(folder, ct).ConfigureAwait(false);
        }
    }

    public async Task UploadAsync(Stream content, string path, CancellationToken ct)
    {
        var target = RemotePaths.Join(_root, path);
        var part = $"{target}.part";

        await _client.UploadFileAsync(content, part, ct).ConfigureAwait(false);

        try
        {
            // OpenSSH's posix-rename replaces the file in one step.
            await Task.Run(() => _client.RenameFile(part, target, isPosix: true), ct)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException ex)
        {
            // A server without it renames only onto nothing: the old file goes first.
            _logger.LogDebug(ex, "The SFTP server can't replace {Path} in one step", target);

            if (await _client.ExistsAsync(target, ct).ConfigureAwait(false))
                await _client.DeleteFileAsync(target, ct).ConfigureAwait(false);

            await _client.RenameFileAsync(part, target, ct).ConfigureAwait(false);
        }
    }

    public async Task DeleteAsync(string path, CancellationToken ct)
    {
        var target = RemotePaths.Join(_root, path);

        if (await _client.ExistsAsync(target, ct).ConfigureAwait(false))
            await _client.DeleteFileAsync(target, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> ListAsync(string path, CancellationToken ct)
    {
        var names = new List<string>();

        await foreach (
            var entry in _client
                .ListDirectoryAsync(RemotePaths.Join(_root, path), ct)
                .ConfigureAwait(false)
        )
        {
            if (entry.Name is not "." and not "..")
                names.Add(entry.Name);
        }

        return names;
    }

    public ValueTask DisposeAsync()
    {
        _client.HostKeyReceived -= OnHostKeyReceived;
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    private void OnHostKeyReceived(object? sender, HostKeyEventArgs e)
    {
        HostKey = FINGERPRINT_PREFIX + e.FingerPrintSHA256;
        e.CanTrust = _trustedHostKey is null || _trustedHostKey == HostKey;
    }
}
