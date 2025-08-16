namespace Turbo.Networking.Codec;

using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;

public class FrameLengthFieldEncoder : MessageToByteEncoder<IByteBuffer>
{
    protected override void Encode(IChannelHandlerContext context, IByteBuffer message, IByteBuffer output)
    {
        output.WriteInt(message.ReadableBytes);
        output.WriteBytes(message);
    }
}
