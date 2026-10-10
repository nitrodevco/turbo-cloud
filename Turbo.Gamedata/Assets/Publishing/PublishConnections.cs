using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Entities.Assets;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Gamedata.Assets.Publishing;

/// <summary>
/// Opens connections to publish targets by their protocol, giving up on one that does not answer
/// within <see cref="AssetBundleConfig.PublishTimeoutSeconds"/>.
/// </summary>
internal sealed class PublishConnections(
    IOptions<AssetBundleConfig> config,
    ILogger<PublishConnections> logger
)
{
    private TimeSpan Timeout =>
        TimeSpan.FromSeconds(Math.Max(1, config.Value.PublishTimeoutSeconds));

    /// <summary>A connection to the target, connected; <paramref name="password"/> is unsealed already.</summary>
    public async Task<IPublishConnection> ConnectAsync(
        AssetPublishTargetEntity target,
        string password,
        CancellationToken ct
    )
    {
        var timeout = Timeout;
        var connection = Create(target, password, timeout);

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);

        limit.CancelAfter(timeout);

        var connected = false;

        try
        {
            await connection.ConnectAsync(limit.Token).ConfigureAwait(false);
            connected = true;

            return connection;
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"{Where(target)} did not answer within {timeout.TotalSeconds:0} seconds.",
                ex
            );
        }
        finally
        {
            if (!connected)
                await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Where the target is, in words: <c>sftp://host:22/path</c>, or the folder.</summary>
    public static string Where(AssetPublishTargetEntity target) =>
        target.Protocol == AssetPublishProtocol.Folder
            ? target.RemotePath
            : $"{target.Protocol.ToString().ToLowerInvariant()}://{target.Host}{(target.Port == 0 ? "" : $":{target.Port}")}/{target.RemotePath.TrimStart('/')}";

    private IPublishConnection Create(
        AssetPublishTargetEntity target,
        string password,
        TimeSpan timeout
    ) =>
        target.Protocol switch
        {
            AssetPublishProtocol.Folder => new FolderPublishConnection(target.RemotePath),
            AssetPublishProtocol.Ftp or AssetPublishProtocol.Ftps => new FtpPublishConnection(
                target.Host,
                target.Port,
                target.User,
                password,
                target.RemotePath,
                secure: target.Protocol == AssetPublishProtocol.Ftps,
                target.AllowSelfSigned,
                timeout
            ),
            AssetPublishProtocol.Sftp => new SftpPublishConnection(
                target.Host,
                target.Port,
                target.User,
                password,
                target.RemotePath,
                target.HostKey,
                timeout,
                logger
            ),
            _ => throw new InvalidOperationException(
                $"{target.Name} has a protocol this server doesn't know."
            ),
        };
}
