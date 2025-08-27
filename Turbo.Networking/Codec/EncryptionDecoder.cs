using System.Collections.Generic;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Turbo.Core.Networking.Encryption;

namespace Turbo.Networking.Codec;

public class EncryptionDecoder(IRc4Service rc4Service) : ByteToMessageDecoder
{
    protected override void Decode(
        IChannelHandlerContext context,
        IByteBuffer message,
        List<object> output
    )
    {
        if (message.ReadableBytes == 0)
            return;

        var data = new byte[message.ReadableBytes];

        message.ReadBytes(data);

        var processedData = rc4Service.ProcessBytes(data);

        if (processedData.Length <= 0)
            return;

        output.Add(Unpooled.WrappedBuffer(processedData));
    }
}