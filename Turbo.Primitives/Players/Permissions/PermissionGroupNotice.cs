using System.Collections.Immutable;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// What to tell a player whose groups changed: a hotel text and its parameters, for
/// <c>IPlayerNoticeService.SendAsync</c> or a command's <c>NotifyAsync</c>, whichever delivers it.
/// </summary>
public sealed record PermissionGroupNotice(
    string TextKey,
    string DefaultText,
    ImmutableArray<string> Parameters
);
