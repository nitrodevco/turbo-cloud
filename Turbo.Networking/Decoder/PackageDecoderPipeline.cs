using System.Buffers;
using System.Collections.Generic;
using Turbo.Networking.Abstractions.Middleware;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Decoder;

internal sealed class PackageDecoderPipeline
{
    private readonly List<IFrameMiddleware> _middleWares = [];

    public PackageDecoderPipeline Use(IFrameMiddleware mw)
    {
        _middleWares.Add(mw);

        return this;
    }

    public void Invoke(
        ref SequenceReader<byte> reader,
        ISessionContext ctx,
        ref IClientPacket? packet
    )
    {
        foreach (var middleWare in _middleWares)
        {
            middleWare.Invoke(ref reader, ctx, ref packet);

            if (packet != null)
                break;
        }
    }
}
