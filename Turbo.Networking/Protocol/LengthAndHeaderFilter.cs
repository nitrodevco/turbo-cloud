using System;
using System.Buffers;
using System.Buffers.Binary;
using SuperSocket.ProtoBase;
using Turbo.Core.Networking.Protocol;

namespace Turbo.Networking.Protocol;

public class LengthAndHeaderFilter : FixedHeaderPipelineFilter<Package>
{
    private const int MaxLength = 500_000;

    public LengthAndHeaderFilter()
        : base(4) { }

    protected override int GetBodyLengthFromHeader(ref ReadOnlySequence<byte> buffer)
    {
        int len = BinaryPrimitives.ReadInt32BigEndian(buffer.FirstSpan); // use LittleEndian if needed
        if (len < 2 || len > MaxLength)
            throw new ProtocolException($"Invalid frame length: {len}");
        return len; // body length == payload bytes
    }

    protected override Package DecodePackage(ref ReadOnlySequence<byte> buffer)
    {
        var reader = new SequenceReader<byte>(buffer);
        reader.TryReadBigEndian(out int length);
        reader.TryReadBigEndian(out short header);

        return new Package(
            PackageType.Client,
            new ClientPacket(header, buffer.Slice(6, length - 2).ToArray())
        );
    }
}
