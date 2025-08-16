namespace Turbo.Networking.Codec;

using System.Collections.Generic;

using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;

using Turbo.Core.Packets.Messages;
using Turbo.Packets.Incoming;

public class GameDecoder : MessageToMessageDecoder<IByteBuffer>
{
    protected override void Decode(IChannelHandlerContext context, IByteBuffer message, List<object> output)
    {
        var header = message.ReadShort();
        var content = message.ReadSlice(message.ReadableBytes).Retain() as IByteBuffer;
        IClientPacket packet = new ClientPacket(header, content);
        output.Add(packet);
    }
}
