using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Channels;
using Turbo.Core.Networking.Encryption;

namespace Turbo.Networking.Codec;

public class EncryptionEncoder(IRc4Service rc4Service) : MessageToByteEncoder<IByteBuffer>
{
    protected override void Encode(
        IChannelHandlerContext context,
        IByteBuffer message,
        IByteBuffer output
    )
    {
        if (message.ReadableBytes == 0)
            return;

        var data = new byte[message.ReadableBytes];

        message.ReadBytes(data);

        var processedData = rc4Service.ProcessBytes(data);

        if (processedData.Length <= 0)
            return;

        output.WriteBytes(processedData);
    }
}