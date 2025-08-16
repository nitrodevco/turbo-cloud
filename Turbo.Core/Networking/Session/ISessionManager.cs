namespace Turbo.Core.Networking.Session;

using System.Threading.Tasks;

using DotNetty.Transport.Channels;

public interface ISessionManager
{
    public bool TryGetSession(IChannelId channelId, out ISessionContext ctx);

    public ISessionContext CreateSession(IChannelHandlerContext ctx);

    public Task KickSessionAsync(IChannelId channelId, SessionKickType kickType = SessionKickType.Requested);

    public bool RemoveSessionById(IChannelId channelId, out ISessionContext session);

    public void PauseReadsOnAll();

    public void ResumeReadsOnAll();
}
