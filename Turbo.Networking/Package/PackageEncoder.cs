using System;
using System.Buffers;
using System.Buffers.Binary;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Plugins.Messages;

namespace Turbo.Networking.Package;

public sealed class PackageEncoder(
    IRevisionManager revisionManager,
    IExtensionPacketRegistry extensions,
    ComposerPayloadCache payloadCache,
    ILogger<PackageEncoder> logger,
    IPluginMessageDirectory? pluginMessages = null
) : IPackageEncoder<OutgoingPackage>
{
    private readonly IRevisionManager _revisionManager = revisionManager;
    private readonly IExtensionPacketRegistry _extensions = extensions;
    private readonly ComposerPayloadCache _payloadCache = payloadCache;
    private readonly ILogger<PackageEncoder> _logger = logger;
    private readonly IPluginMessageDirectory? _pluginMessages = pluginMessages;

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

            // A plugin's packet a room sent is already written; only the copy is encrypted.
            if (composer is WrittenExtensionPacket written)
            {
                var bytes = writer.GetSpan(written.Bytes.Length)[..written.Bytes.Length];

                written.Bytes.AsSpan().CopyTo(bytes);
                session.CryptoOut?.ProcessInPlace(bytes);
                writer.Advance(bytes.Length);

                return bytes.Length;
            }

            if (composer is PluginMessageComposer message)
                return EncodePluginMessage(writer, session, message);

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

    // A plugin message is written already; it is framed here with the header its name has now,
    // and dropped when its plugin has gone since it was sent.
    private int EncodePluginMessage(
        IBufferWriter<byte> writer,
        ISessionContext session,
        PluginMessageComposer message
    )
    {
        if (_pluginMessages is null || !_pluginMessages.TryGetHeader(message.Name, out var header))
        {
            _logger.LogWarning(
                "Plugin message {Name} is not registered; not sent to {SessionKey}",
                message.Name,
                session.SessionKey
            );

            return 0;
        }

        var length = 4 + 2 + message.Body.Length;
        var span = writer.GetSpan(length)[..length];

        BinaryPrimitives.WriteInt32BigEndian(span, length - 4);
        BinaryPrimitives.WriteInt16BigEndian(span[4..], (short)header);
        message.Body.AsSpan().CopyTo(span[6..]);
        session.CryptoOut?.ProcessInPlace(span);
        writer.Advance(length);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Outgoing plugin message {Name} as {Header}", message.Name, header);

        return length;
    }
}
