namespace Turbo.Core.Networking.Session;

using System;
using System.Threading.Tasks;

using DotNetty.Transport.Channels;

public interface ISessionContext
{
    public IChannel Channel { get; }

    public IChannelId ChannelId { get; }

    public long PlayerId { get; }

    public string RevisionId { get; }

    public bool IsAuthenticated { get; }

    public void SetRevision(string revisionId);

    public void AttachPlayer(long playerId);

    public Task DisposeAsync();

    public ValueTask SendAsync(ReadOnlyMemory<byte> payload);

    public void PauseReads();

    public void ResumeReads();
}
