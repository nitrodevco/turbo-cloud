namespace Turbo.Core.Networking.Dispatcher;

using DotNetty.Transport.Channels;

public interface IRateLimiter
{
    bool TryAcquire(IChannelId channelId, out bool exceededStrikeLimit);

    void Reset(IChannelId channelId);
}
