using System;
using System.Buffers;
using System.Threading.Tasks;
using SuperSocket.ProtoBase;
using Turbo.Core.Networking.Protocol;
using Turbo.Core.Networking.Session;
using Turbo.Core.Packets.Messages;
using Turbo.Networking.Middleware;
using Turbo.Packets.Incoming;

namespace Turbo.Networking.Pipeline;

public class PipelineFilter : PipelineFilterBase<IClientPacket>
{
    private readonly FramePipelineBuilder _pipeline = new FramePipelineBuilder()
        .Use(new FlashPolicyMiddleware())
        .Use(new DecryptionMiddleware())
        .Use(new LengthFieldMiddleware())
        .Use(new ClientPacketMiddleware());

    public override IClientPacket Filter(ref SequenceReader<byte> reader)
    {
        var session = (ISessionContext)Context;

        if (session is null)
            return null;

        var r = reader;
        IClientPacket? packet = null;

        _pipeline.Invoke(ref r, session, ref packet);

        if (packet is null)
            return null;

        reader = r;

        Reset();

        return packet;
    }

    public override void Reset() { }
}
