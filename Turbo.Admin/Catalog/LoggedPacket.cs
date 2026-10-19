namespace Turbo.Admin.Catalog;

/// <summary>
/// One packet of a packet log: its name when the log gives one, whether the client received it,
/// its header, what came after the header, and the line of the log it starts on. A packet the
/// log holds but that could not be read has no <see cref="Payload"/>, and
/// <see cref="Problem"/> says why.
/// </summary>
public sealed record LoggedPacket(
    string? Name,
    bool Incoming,
    int Header,
    byte[]? Payload,
    string? Problem,
    int Line
);
