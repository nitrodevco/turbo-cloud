using System;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;

namespace Turbo.Core.Networking.Session;

public interface ISessionContext
{
    public IChannel Channel { get; }
    public IChannelId ChannelId { get; }
    public long PlayerId { get; }
    public bool IsAuthenticated { get; }
    public void AttachPlayer(long playerId);
    public Task DisposeAsync();
    public ValueTask SendAsync(ReadOnlyMemory<byte> payload);
    public void PauseReads();
    public void ResumeReads();
}