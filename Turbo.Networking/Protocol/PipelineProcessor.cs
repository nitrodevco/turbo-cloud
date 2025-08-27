using System;
using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Core.Networking.Protocol;

namespace Turbo.Networking.Protocol;

public class PipelineProcessor : PipelineFilterBase<Package>
{
    private const int MaxPolicyBytes = 1_024;

    public override Package Filter(ref SequenceReader<byte> reader)
    {
        if (reader.Remaining < 1)
            return default;

        reader.TryRead(out byte first);
        reader.Rewind(1);

        if (first == (byte)'<')
        {
            // Read through NUL; if not fully available yet, wait
            if (!TryConsumeUntilNul(ref reader, MaxPolicyBytes))
                return default;

            // Emit a PolicyRequest package (no payload needed)
            return new Package(PackageType.Policy, null);
        }

        // Not policy → hand off to the real frame decoder
        NextFilter = new LengthAndHeaderFilter();
        return default;
    }

    private static bool TryConsumeUntilNul(ref SequenceReader<byte> reader, int max)
    {
        int consumed = 0;
        while (reader.TryRead(out byte b))
        {
            consumed++;
            if (consumed > max)
                throw new ProtocolException("Policy line too long");
            if (b == 0x00)
                return true; // done
        }
        // Not enough data; rewind consumption
        reader.Rewind(consumed);
        return false;
    }

    public override void Reset() { }
}
