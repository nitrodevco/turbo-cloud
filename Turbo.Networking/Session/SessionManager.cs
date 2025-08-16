using System;
using System.Collections.Concurrent;
using DotNetty.Transport.Channels;
using Turbo.Core.Networking.Session;

namespace Turbo.Networking.Session;

public class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<IChannelId, ISessionContext> _sessions = new();

    public bool TryGetSession(IChannelId channelId, out ISessionContext ctx) => _sessions.TryGetValue(channelId, out ctx!);

    public ISessionContext CreateSession(IChannelHandlerContext ctx)
    {
        var session = new SessionContext(ctx);

        _sessions[session.ChannelId] = session;

        return session;
    }

    public void RemoveSessionById(IChannelId channelId) => _sessions.TryRemove(channelId, out _);

    public void PauseReadsOnAll()
    {
        foreach (var session in _sessions.Values) session.PauseReads();
    }

    public void ResumeReadsOnAll()
    {
        foreach (var session in _sessions.Values) session.ResumeReads();
    }
}