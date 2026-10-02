using System;
using System.Collections.Frozen;

namespace Turbo.Primitives.Commands;

/// <summary>
/// The <c>:words</c> the AIR / Flash client runs itself, so the server never sees them. The set
/// grows with the client's version, so registering one of these names is allowed but warned: it
/// will not work for players on that client. <c>kick</c> and <c>mute</c> are absent on purpose:
/// that client sends them to the server from security level 4.
/// </summary>
public static class ClientSwallowedCommands
{
    public static FrozenSet<string> Names { get; } =
        new[]
        {
            "shake",
            "sign",
            "chooser",
            "furni",
            "zoom",
            "visit",
            "roomid",
            "ignore",
            "floor",
            "fps",
            "screenshot",
            "wired",
            "lang",
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
