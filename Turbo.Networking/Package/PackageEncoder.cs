using System;
using System.Buffers;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;

namespace Turbo.Networking.Package;

public sealed class PackageEncoder(
    IRevisionManager revisionManager,
    IExtensionPacketRegistry extensions,
    ComposerPayloadCache payloadCache,
    ILogger<PackageEncoder> logger
) : IPackageEncoder<OutgoingPackage>
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IExtensionPacketRegistry _extensions = extensions;
    private readonly ComposerPayloadCache _payloadCache = payloadCache;
    private readonly ILogger<PackageEncoder> _logger = logger;

    public int Encode(IBufferWriter<byte> writer, OutgoingPackage pack) =>
        Encode(writer, pack.Session, pack.Composer);

    /// <summary>
    /// Writes one composer, framed and encrypted for <paramref name="session"/>, and returns the
    /// number of bytes written. A composer that cannot be written is logged and writes nothing,
    /// so the rest of a batch still goes out and the session's key stream is not advanced.
    /// </summary>
    public int Encode(IBufferWriter<byte> writer, ISessionContext session, IComposer composer)
    {
        try
        {
            if (session.Connection?.IsClosed ?? true)
                return 0;

            var revision = _revisionManager.GetRevision(session.RevisionId);

            if (revision is null)
                return 0;

            var composerType = composer.GetType();

            // Core first; the plugin overlay is only asked about a type the revision lacks.
            if (
                !revision.Serializers.TryGetValue(composerType, out var serializer)
                && !_extensions.TryGetSerializer(composerType, out serializer!)
            )
            {
                _logger.LogWarning(
                    "Serializer not found for {Name} for {SessionKey}",
                    composerType.Name,
                    session.SessionKey
                );

                return 0;
            }

            var payload = _payloadCache.GetOrSerialize(serializer, composer);
            var span = writer.GetSpan(payload.Length)[..payload.Length];

            // The cached bytes are shared by every recipient; only the copy is encrypted.
            payload.CopyTo(span);
            session.CryptoOut?.ProcessInPlace(span);
            writer.Advance(payload.Length);

            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("Outgoing {Composer}", composer);

            return payload.Length;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to serialize packet {Packet} for session {SessionKey}",
                composer.GetType().Name,
                session.SessionKey
            );
        }

        return 0;
    }
}
