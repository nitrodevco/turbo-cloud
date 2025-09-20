using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Middleware;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Pipeline;

internal sealed class PipelineFilter : PipelineFilterBase<IClientPacket>
{
    private readonly FramePipelineBuilder _pipeline = new FramePipelineBuilder()
        .Use(new FlashPolicyMiddleware())
        .Use(new EncryptionMiddleware())
        .Use(new LengthFieldMiddleware())
        .Use(new ClientPacketMiddleware());

    public override IClientPacket? Filter(ref SequenceReader<byte> reader)
    {
        if (Context is ISessionContext session)
        {
            var r = reader;
            IClientPacket? packet = null;

            _pipeline.Invoke(ref r, session, ref packet);

            if (packet is not null)
            {
                reader = r;

                Reset();

                return packet;
            }
        }

        return null;
    }

    public override void Reset() { }
}
