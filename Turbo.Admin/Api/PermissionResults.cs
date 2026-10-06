using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Permissions;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>What the permission endpoints share: the view gate, durations, and their answers.</summary>
internal static class PermissionResults
{
    private const string NO_ACCESS = "You can't see permissions.";

    /// <summary>Every permission endpoint needs <c>admin.permissions.view</c>; changes need more, checked by each.</summary>
    public static Func<
        EndpointFilterInvocationContext,
        EndpointFilterDelegate,
        ValueTask<object?>
    > RequireView(IGrainFactory grainFactory) =>
        async (context, next) =>
        {
            var http = context.HttpContext;

            return await grainFactory
                .HasPermissionAsync(
                    AdminIdentity.Of(http).PlayerId,
                    PermissionNodes.Admin.PERMISSIONS_VIEW,
                    http.RequestAborted
                )
                .ConfigureAwait(false)
                ? await next(context).ConfigureAwait(false)
                : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
        };

    /// <summary>
    /// When a duration typed as <c>30m</c>, <c>12h</c>, <c>7d</c> or <c>2w</c> ends, from now;
    /// null for none given (or <c>perm</c>), which is permanent. False for one that cannot be read.
    /// </summary>
    public static bool TryExpiry(string? duration, DateTime now, out DateTime? expiresAt)
    {
        expiresAt = null;

        if (string.IsNullOrWhiteSpace(duration))
            return true;

        if (!CommandDuration.TryParse(duration, out var parsed))
            return false;

        expiresAt = parsed.EndsAt(now);

        return true;
    }

    /// <summary>
    /// How long a duration typed as <c>30m</c>, <c>12h</c>, <c>7d</c> or <c>2w</c> lasts; null for
    /// none given (or <c>perm</c>), which is permanent. False for one that cannot be read.
    /// </summary>
    public static bool TryDuration(string? duration, out TimeSpan? span)
    {
        span = null;

        if (string.IsNullOrWhiteSpace(duration))
            return true;

        if (!CommandDuration.TryParse(duration, out var parsed))
            return false;

        span = parsed.Span;

        return true;
    }

    public static IResult BadDuration(string? duration) =>
        AdminResults.Error(
            StatusCodes.Status400BadRequest,
            $"'{duration}' is not a duration like 30m, 12h, 7d or 2w."
        );

    public static PermissionExpiryModeType ModeOf(bool extend) =>
        extend ? PermissionExpiryModeType.Extend : PermissionExpiryModeType.Replace;

    public static IResult Refused(
        PermissionEditRefusal refusal,
        PermissionEditor editor,
        string what
    ) =>
        AdminResults.Error(
            StatusCodes.Status403Forbidden,
            refusal switch
            {
                PermissionEditRefusal.NeedsManageNode => "You can't change permissions.",
                PermissionEditRefusal.GroupTooHeavy =>
                    $"Only groups lighter than your heaviest (weight {editor.HeaviestWeight}) are yours to change.",
                PermissionEditRefusal.NeedsSuperuser =>
                    "That group gives permissions.superuser, so only a superuser can change or hand it out.",
                PermissionEditRefusal.WouldLoseSuperuser =>
                    "That would take permissions.superuser from you. Make someone else a superuser first; they can then remove yours, or use the server console.",
                PermissionEditRefusal.PlayerTooHeavy =>
                    $"{what} is in a group as heavy as your heaviest or heavier, so their permissions are not yours to change.",
                PermissionEditRefusal.NodeNotHeld => PermissionNodeFormat.IsWildcard(what)
                    ? $"You can only grant or deny {what} if you hold every node it covers."
                    : $"You can only grant or deny {what} if you hold it yourself.",
                _ => "You can't do that.",
            }
        );

    /// <summary>The grain's answer to a change, as the panel shows it.</summary>
    public static IResult Changed(PermissionChangeResultType result) =>
        result switch
        {
            PermissionChangeResultType.Changed => Results.Ok(
                new PermissionChangeResponse(true, "Done.")
            ),
            PermissionChangeResultType.Unchanged => Results.Ok(
                new PermissionChangeResponse(false, "Already so; nothing changed.")
            ),
            PermissionChangeResultType.UnknownGroup => AdminResults.Error(
                StatusCodes.Status404NotFound,
                "There is no such group."
            ),
            PermissionChangeResultType.NotFound => AdminResults.Error(
                StatusCodes.Status404NotFound,
                "Nothing of that name was set."
            ),
            PermissionChangeResultType.Invalid => AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Nodes and keys are lowercase dotted segments (a-z, 0-9, _), a wildcard ends in .*, and a group name is one segment."
            ),
            PermissionChangeResultType.Expired => AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "That expiry has already passed."
            ),
            PermissionChangeResultType.ReservedNode => AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "group.<name> comes from holding the group; add the player to it instead."
            ),
            PermissionChangeResultType.ProtectedGroup => AdminResults.Error(
                StatusCodes.Status409Conflict,
                "The default group cannot be deleted, joined or left."
            ),
            PermissionChangeResultType.WouldCycle => AdminResults.Error(
                StatusCodes.Status409Conflict,
                "That parent would make the group inherit from itself."
            ),
            PermissionChangeResultType.AlreadyExists => AdminResults.Error(
                StatusCodes.Status409Conflict,
                "A group with that name already exists."
            ),
            _ => AdminResults.Error(StatusCodes.Status400BadRequest, result.ToString()),
        };
}
