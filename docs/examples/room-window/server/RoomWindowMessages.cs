using System.Collections.Immutable;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Plugins.Messages;

namespace RoomWindow;

// The named messages between the plugin and its client module (docs/plugin-messages.md). The
// client reads and writes them by the schema the server sends it, so neither side names a header.

/// <summary>Client to server: "I am in a room; show me where its windows look".</summary>
[PluginMessage("roomwindow.look")]
public sealed record Look : IPluginMessage;

/// <summary>
/// One window of the room the player is in: room <paramref name="RoomId"/> is drawn so that its
/// tile (0, 0) sits on this room's tile (<paramref name="OffsetX"/>, <paramref name="OffsetY"/>),
/// which puts the far half of the pair just behind this half.
/// </summary>
public sealed record WindowView(int WindowId, int RoomId, int OffsetX, int OffsetY);

/// <summary>Server to client: every window of the room the player is in (none: draw nothing).</summary>
[PluginMessage("roomwindow.views")]
public sealed record Views(ImmutableArray<WindowView> Windows) : IPluginMessage;

/// <summary>
/// Server to client, the watch's envelope: one of room <paramref name="RoomId"/>'s own packets,
/// carried whole in the last field. The watch fills <paramref name="Carried"/>; we never do.
/// </summary>
[PluginMessage("roomwindow.room")]
public sealed record WindowRoom(int RoomId, IComposer? Carried = null) : IPluginMessage;

/// <summary>Client to server: the player clicked tile (<paramref name="X"/>, <paramref name="Y"/>) of the room behind a window.</summary>
[PluginMessage("roomwindow.cross")]
public sealed record Cross(int WindowId, int X, int Y) : IPluginMessage;
