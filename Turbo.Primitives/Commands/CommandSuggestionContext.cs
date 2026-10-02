using System.Collections.Generic;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Commands;

/// <summary>Read-only advisory input. Authority comes from Permissions; earlier text is untrusted client input.</summary>
public sealed record CommandSuggestionContext(
    PlayerId PlayerId,
    RoomId? RoomId,
    ResolvedPermissionsSnapshot Permissions,
    string Command,
    string Syntax,
    IReadOnlyDictionary<string, string> Arguments
);
