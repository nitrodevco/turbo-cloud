using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using Turbo.Core.Packets.Messages;
using Turbo.Core.Packets.Revisions;

namespace Turbo.Core.Networking.Session;

public interface ISessionContext
{
    public IChannel Channel { get; }

    public IChannelId ChannelId { get; }
    public IRevision Revision { get; }

    public long PlayerId { get; }

    public bool IsAuthenticated { get; }

    public void SetRevision(IRevision revision);
    public void AddEncryption(byte[] sharedKey);

    public void AttachPlayer(long playerId);

    public Task DisposeAsync();

    public ValueTask SendAsync(IComposer composer, CancellationToken ct = default);
    public ValueTask SendManyAsync(
        IEnumerable<IComposer> composers,
        CancellationToken ct = default
    );

    public void PauseReads();

    public void ResumeReads();
}
