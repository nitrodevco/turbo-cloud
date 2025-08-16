using DotNetty.Transport.Channels;

namespace Turbo.Core.Networking.Dispatcher;

public interface IRateLimiter
{
    bool TryAcquire(IChannelId channelId, out bool exceededStrikeLimit);

    void Reset(IChannelId channelId);
}
