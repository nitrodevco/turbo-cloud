namespace Turbo.Primitives.Networking.Enums;

/// <summary>What <see cref="IClientPacketDecoder.TryRead"/> found at the front of what a client sent.</summary>
public enum ClientPacketReadResult
{
    /// <summary>A whole packet, read and consumed.</summary>
    Packet,

    /// <summary>The next packet has not all arrived yet; nothing was consumed.</summary>
    Incomplete,

    /// <summary>
    /// The next packet declares a length no packet can have. The session is being closed and
    /// nothing more is read from it.
    /// </summary>
    Refused,
}
