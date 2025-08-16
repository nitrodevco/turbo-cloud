using DotNetty.Transport.Channels;
using Microsoft.Extensions.Hosting;
using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;

namespace Turbo.Core.Networking;

public interface IPacketDispatcher : IHostedService
{
    public bool TryEnqueue(IChannelId channelId, IClientPacket packet, out PacketRejectType rejectType);
}