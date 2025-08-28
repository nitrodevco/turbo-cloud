using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;

namespace Turbo.Networking.Pipeline;

public class FramePipelineBuilder
{
    private readonly List<IFrameMiddleware> _middleWares = [];

    public FramePipelineBuilder Use(IFrameMiddleware mw)
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
