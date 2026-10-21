using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// What the content endpoints share (<see cref="ContentEndpoints"/>, the polls and the quests):
/// seeing needs <c>admin.content.view</c> and changing <c>content.manage</c> as well, declared on
/// each group with <see cref="Require"/>, and a change the game refuses answers 400 with why.
/// </summary>
internal sealed class ContentAccess(IGrainFactory grainFactory)
{
    public const string NO_ACCESS = "You can't see the game's content.";
    public const string NO_MANAGE = "You can't change the game's content.";

    /// <summary>Who may see and change a content group.</summary>
    public static RouteGroupBuilder Require(RouteGroupBuilder group) =>
        group
            .RequirePermission(PermissionNodes.Admin.CONTENT_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Content.MANAGE, NO_MANAGE);

    /// <summary>Who made a change, for the log: the panel and the staff member's name.</summary>
    public static string Actor(HttpContext http) => $"panel:{AdminIdentity.Of(http).Name}";

    /// <summary>Whether the viewer may change the content, for a list that says so.</summary>
    public Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HoldsAsync(http, PermissionNodes.Content.MANAGE, ct);

    /// <summary>A change: what it answers, or 400 with why the game refused it.</summary>
    public static async Task<IResult> ChangeAsync(Func<Task<IResult>> change)
    {
        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }
}
