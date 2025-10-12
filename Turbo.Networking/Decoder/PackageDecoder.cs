using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Networking.Abstractions.Session;
using Turbo.Packets.Abstractions;

namespace Turbo.Networking.Decoder;

internal sealed class PackageDecoder : PipelineFilterBase<IClientPacket>
{
    private readonly PackageDecoderPipeline _pipeline = new PackageDecoderPipeline()
        .Use(new FlashPolicyDecoder())
        .Use(new EncryptionDecoder())
        .Use(new LengthFieldDecoder())
        .Use(new ClientPacketDecoder());

    public override IClientPacket Filter(ref SequenceReader<byte> reader)
    {
        var r = reader;
        IClientPacket? packet = null;

        if (Context is ISessionContext session)
            _pipeline.Invoke(ref r, session, ref packet);

        reader = r;

        Reset();

        return packet!;
    }

    public override void Reset() { }
}
