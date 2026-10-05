using System;
using Turbo.Primitives.Packets;

namespace Turbo.Primitives.Networking.Extensions;

/// <summary>
/// Where plugins add parsers, serializers and capabilities to Turbo's protocol, beside every
/// revision's own. Core is always consulted first, so a plugin cannot replace a core packet.
/// Each <c>Register*</c> returns a handle that removes the registration; a plugin registers in
/// <c>StartAsync</c> and disposes the handles in <c>StopAsync</c>. See
/// <c>docs/plugin-packets.md</c>.
/// </summary>
public interface IExtensionPacketRegistry
{
    /// <summary>Reads incoming packets with <paramref name="header"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The header is not a plugin header.</exception>
    /// <exception cref="InvalidOperationException">The header already has a parser.</exception>
    IDisposable RegisterParser(int header, IParser parser);

    /// <summary>Writes <paramref name="composerType"/> with <paramref name="serializer"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The serializer's header is not a plugin header.</exception>
    /// <exception cref="InvalidOperationException">The type already has a serializer.</exception>
    IDisposable RegisterSerializer(Type composerType, ISerializer serializer);

    /// <summary>Lets a client ask for <paramref name="name"/>; see <c>ClientCapabilities.Negotiate</c>.</summary>
    /// <exception cref="ArgumentException">The name is empty or a core capability.</exception>
    /// <exception cref="InvalidOperationException">A plugin already registered the name.</exception>
    IDisposable RegisterCapability(string name);

    bool TryGetParser(int header, out IParser parser);

    bool TryGetSerializer(Type composerType, out ISerializer serializer);

    bool HasCapability(string name);
}
