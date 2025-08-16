using DotNetty.Transport.Channels;

namespace Turbo.Core.Packets.Messages;

public readonly record struct PacketEnvelope(IChannelId ChannelId, IClientPacket Msg);
