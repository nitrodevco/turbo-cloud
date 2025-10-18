using System.Buffers;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using Turbo.Networking.Abstractions.Revisions;

namespace Turbo.Networking.Encoder;

public sealed class PackageEncoder(IRevisionManager revisionManager, ILogger<PackageEncoder> logger)
    : IPackageEncoder<OutgoingPackage>
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly ILogger<PackageEncoder> _logger = logger;

    public int Encode(IBufferWriter<byte> writer, OutgoingPackage pack)
    {
        var revision = _revisionManager.GetRevision(pack.Session.RevisionId);

        if (revision is not null)
        {
            var composerType = pack.Composer.GetType();

            if (revision.Serializers.TryGetValue(composerType, out var serializer))
            {
                var payload = serializer.Serialize(pack.Composer).ToArray();

                if (pack.Session.CryptoOut is not null)
                    payload = pack.Session.CryptoOut.Process(payload);

                _logger.LogInformation(
                    "Outgoing {Composer} for {SessionId}",
                    pack.Composer,
                    pack.Session.SessionID
                );

                writer.Write(payload);

                return payload.Length;
            }
            else
            {
                _logger.LogWarning(
                    "Serializer not found for {Name} for {SessionId}",
                    composerType.Name,
                    pack.Session.SessionID
                );
            }
        }

        return 0;
    }
}
