using DotNetty.Transport.Channels;

namespace Turbo.Core.Networking.Session;

public interface ISessionManager
{
    public bool TryGetSession(IChannelId channelId, out ISessionContext ctx);
    public ISessionContext CreateSession(IChannelHandlerContext ctx);
    public void RemoveSessionById(IChannelId channelId);
    public void PauseReadsOnAll();
    public void ResumeReadsOnAll();
}