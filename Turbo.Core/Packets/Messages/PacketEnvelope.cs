namespace Turbo.Core.Packets.Messages;

using DotNetty.Transport.Channels;

public readonly record struct PacketEnvelope(IChannelId ChannelId, IClientPacket Msg);
