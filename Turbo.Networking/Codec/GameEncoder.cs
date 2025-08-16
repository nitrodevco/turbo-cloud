namespace Turbo.Networking.Codec;

using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Turbo.Core.Packets.Messages;
using Turbo.Packets.Outgoing;

public class GameEncoder : MessageToByteEncoder<IServerPacket>
{
    protected override void Encode(IChannelHandlerContext context, IServerPacket message, IByteBuffer output)
    {
        output.WriteBytes(message.Content);
    }
}
