using System.Buffers;
using SuperSocket.ProtoBase;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Networking.Encoder;

public sealed class PackageEncoder(IRevisionManager revisionManager)
    : IPackageEncoder<OutgoingPackage>
{
    private readonly IRevisionManager _revisionManager = revisionManager;

    public int Encode(IBufferWriter<byte> writer, OutgoingPackage pack)
    {
        var revision = _revisionManager.GetRevision(pack.Session.RevisionId);

        if (revision is not null)
        {
            if (revision.Serializers.TryGetValue(pack.Composer.GetType(), out var serializer))
            {
                var payload = serializer.Serialize(pack.Composer).ToArray();

                if (pack.Session.CryptoOut is not null)
                    payload = pack.Session.CryptoOut.Process(payload);

                writer.Write(payload);

                return payload.Length;
            }
        }

        return 0;
    }
}
