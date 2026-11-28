using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Networking.Revisions;
using Turbo.Primitives.Packets;
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

    // Each revision's feature serializers by composer type, built the first time a session with
    // a feature is written to: a send then costs one lookup, and only for such a session.
    private readonly ConditionalWeakTable<
        IRevision,
        FrozenDictionary<Type, FeatureSerializer[]>
    > _featureSerializers = [];

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

            if (composer is PluginEnvelopeComposer envelope)
                return EncodeEnvelope(writer, session, revision, envelope);

            var framed = Frame(session, revision, composer);

            if (framed.IsEmpty)
                return 0;

            var span = writer.GetSpan(framed.Length)[..framed.Length];

            // The framed bytes may be shared by every recipient; only the copy is encrypted.
            framed.Span.CopyTo(span);
            session.CryptoOut?.ProcessInPlace(span);
            writer.Advance(span.Length);

            return span.Length;
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

    /// <summary>
    /// The composer framed (length, header, body) and not encrypted, or empty when it cannot be
    /// written (logged). The bytes may be shared: never write to them.
    /// </summary>
    private ReadOnlyMemory<byte> Frame(
        ISessionContext session,
        IRevision revision,
        IComposer composer
    )
    {
        // A plugin's packet a room sent is already written.
        if (composer is WrittenExtensionPacket written)
            return written.Bytes.AsMemory();

        if (composer is PluginMessageComposer message)
            return FramePluginMessage(session, message);

        if (composer is PluginEnvelopeComposer)
        {
            _logger.LogWarning(
                "An envelope carries another envelope; not sent to {SessionKey}",
                session.SessionKey
            );

            return ReadOnlyMemory<byte>.Empty;
        }

        var composerType = composer.GetType();

        // A layout for a feature this session's client named in its hello comes first, then
        // core; the plugin overlay is only asked about a type the revision lacks.
        if (
            !TryGetFeatureSerializer(session, revision, composerType, out var serializer)
            && !revision.Serializers.TryGetValue(composerType, out serializer)
            && !_extensions.TryGetSerializer(composerType, out serializer!)
        )
        {
            _logger.LogWarning(
                "Serializer not found for {Name} for {SessionKey}",
                composerType.Name,
                session.SessionKey
            );

            return ReadOnlyMemory<byte>.Empty;
        }

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Outgoing {Composer}", composer);

        // The cached bytes are shared by every recipient this serializer writes for: the cache
        // keys them by serializer as well as composer, so a feature's layout and the plain one
        // are held apart.
        return _payloadCache.GetOrSerialize(serializer, composer);
    }

    /// <summary>
    /// The serializer of a protocol feature <paramref name="session"/>'s client named, when the
    /// revision has one for <paramref name="composerType"/>. Of two features claiming one type,
    /// the name that sorts first (ordinal) writes it.
    /// </summary>
    private bool TryGetFeatureSerializer(
        ISessionContext session,
        IRevision revision,
        Type composerType,
        [MaybeNullWhen(false)] out ISerializer serializer
    )
    {
        var features = session.ClientFeatures;

        if (
            features.Count > 0
            && _featureSerializers
                .GetValue(revision, static x => IndexFeatureSerializers(x))
                .TryGetValue(composerType, out var candidates)
        )
        {
            foreach (var candidate in candidates)
            {
                if (!features.Contains(candidate.Feature))
                    continue;

                serializer = candidate.Serializer;

                return true;
            }
        }

        serializer = null;

        return false;
    }

    private static FrozenDictionary<Type, FeatureSerializer[]> IndexFeatureSerializers(
        IRevision revision
    ) =>
        revision
            .FeatureSerializers.SelectMany(feature =>
                feature.Value.Select(x =>
                    (Type: x.Key, Entry: new FeatureSerializer(feature.Key, x.Value))
                )
            )
            .GroupBy(x => x.Type)
            .ToFrozenDictionary(
                x => x.Key,
                x =>
                    x.Select(y => y.Entry).OrderBy(y => y.Feature, StringComparer.Ordinal).ToArray()
            );

    private readonly record struct FeatureSerializer(string Feature, ISerializer Serializer);

    // A plugin message is written already; it is framed here with the header its name has now,
    // and dropped when its plugin has gone since it was sent.
    private ReadOnlyMemory<byte> FramePluginMessage(
        ISessionContext session,
        PluginMessageComposer message
    )
    {
        if (!TryGetHeader(session, message.Name, out var header))
            return ReadOnlyMemory<byte>.Empty;

        var framed = new byte[4 + 2 + message.Body.Length];

        BinaryPrimitives.WriteInt32BigEndian(framed, framed.Length - 4);
        BinaryPrimitives.WriteInt16BigEndian(framed.AsSpan(4), (short)header);
        message.Body.AsSpan().CopyTo(framed.AsSpan(6));

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Outgoing plugin message {Name} as {Header}", message.Name, header);

        return framed;
    }

    /// <summary>
    /// The envelope's plugin message (its header and fields) followed by the packet it carries
    /// as this session's encoder frames it, less its length. The carried packet's bytes come
    /// from the same cache a room's broadcast does, so only this frame is written per session.
    /// </summary>
    private int EncodeEnvelope(
        IBufferWriter<byte> writer,
        ISessionContext session,
        IRevision revision,
        PluginEnvelopeComposer envelope
    )
    {
        if (!TryGetHeader(session, envelope.Outer.Name, out var header))
            return 0;

        var inner = Frame(session, revision, envelope.Inner);

        if (inner.IsEmpty)
            return 0;

        var fields = envelope.Outer.Body.AsSpan();
        var carried = inner.Span[4..];
        var length = 4 + 2 + fields.Length + carried.Length;
        var span = writer.GetSpan(length)[..length];

        BinaryPrimitives.WriteInt32BigEndian(span, length - 4);
        BinaryPrimitives.WriteInt16BigEndian(span[4..], (short)header);
        fields.CopyTo(span[6..]);
        carried.CopyTo(span[(6 + fields.Length)..]);
        session.CryptoOut?.ProcessInPlace(span);
        writer.Advance(length);

        return length;
    }

    private bool TryGetHeader(ISessionContext session, string name, out int header)
    {
        if (_pluginMessages is not null && _pluginMessages.TryGetHeader(name, out header))
            return true;

        _logger.LogWarning(
            "Plugin message {Name} is not registered; not sent to {SessionKey}",
            name,
            session.SessionKey
        );

        header = 0;

        return false;
    }
}
