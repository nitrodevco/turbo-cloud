using DotNetty.Transport.Channels;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking.Pipeline;

public interface IIngressPipeline
{
    bool TryAccept(
        PacketEnvelope envelope,
        out PacketRejectType rejectType,
        out IngressToken token
    );
    void OnProcessed(IngressToken token);
    void Reset(IChannelId channelId);
    IngressToken GetOrCreateToken(IChannelId channelId);
}
