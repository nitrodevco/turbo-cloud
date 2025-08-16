namespace Turbo.Core.Networking.Dispatcher;

using System.Threading.Tasks;

using DotNetty.Transport.Channels;

using Microsoft.Extensions.Hosting;

using Turbo.Core.Packets;
using Turbo.Core.Packets.Messages;

public interface IPacketDispatcher : IHostedService
{
    public bool TryEnqueue(IChannelId channelId, IClientPacket packet, out PacketRejectType rejectType);

    public void ResetForChannelId(IChannelId channelId);
}
