using System.Buffers;
using System.Collections.Generic;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Middleware;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Pipeline;

public class FramePipelineBuilder
{
    private readonly List<FrameMiddleware> _middleWares = [];

    public FramePipelineBuilder Use(FrameMiddleware mw)
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
