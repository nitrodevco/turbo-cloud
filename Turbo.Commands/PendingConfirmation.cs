using System;
using System.Collections.Generic;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Commands;

/// <summary>
/// A prepared command waiting for its executor's <c>:confirm</c>. Its descriptor, bound arguments
/// and captured audiences are retained; execution uses the confirming executor's current rights.
/// </summary>
internal sealed record PendingConfirmation(
    CommandDescriptor Descriptor,
    RoomId? RoomId,
    string ArgumentText,
    object Arguments,
    string? RequiredPermission,
    Guid ExecutionId,
    IReadOnlyDictionary<string, IReadOnlyList<PlayerId>> Audiences,
    DateTime ExpiresAtUtc
);
