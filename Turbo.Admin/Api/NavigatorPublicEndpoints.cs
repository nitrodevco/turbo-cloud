using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;

namespace Turbo.Admin.Api;

/// <summary>
/// The navigator's Public tab (<c>/api/content/navigator/public</c>): its categories, each one
/// block of Habbo's <c>official_view</c>, and the rooms in them in the order staff set. Seeing it
/// is the content view; changing it needs <c>content.manage</c> and
/// <c>navigator.public.manage</c>, and every change is in the command log. Players see a change
/// with their next search; Habbo pushes none.
/// </summary>
internal sealed class NavigatorPublicEndpoints(IPublicRoomEditor editor, AdminActionLog actions)
{
    private const string NO_MANAGE = "You can't change the navigator's Public tab.";
    private const string KIND_MANUAL = "manual";
    private const string KIND_STAFF_PICKS = "staffpicks";

    public void Map(RouteGroupBuilder secured)
    {
        var group = ContentAccess
            .Require(secured.MapGroup("/content/navigator/public"))
            .RequirePermissionToChange(PermissionNodes.Navigator.PUBLIC_MANAGE, NO_MANAGE);

        group.MapGet("", ListAsync);
        group.MapPost(
            "/categories",
            (NavigatorPublicCategoryRequest request, HttpContext http, CancellationToken ct) =>
                SaveCategoryAsync(0, request, http, ct)
        );
        group.MapPut(
            "/categories/{id:int}",
            (
                int id,
                NavigatorPublicCategoryRequest request,
                HttpContext http,
                CancellationToken ct
            ) => SaveCategoryAsync(id, request, http, ct)
        );
        group.MapDelete(
            "/categories/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ChangeAsync(
                    http,
                    $"category delete {id}",
                    async () => await editor.DeleteCategoryAsync(id, ct).ConfigureAwait(false),
                    removed => removed ? Results.NoContent() : Results.NotFound()
                )
        );
        group.MapPut(
            "/categories/order",
            (NavigatorPublicOrderRequest request, HttpContext http, CancellationToken ct) =>
                ChangeAsync(
                    http,
                    $"category order {Ids(request)}",
                    async () =>
                    {
                        await editor
                            .OrderCategoriesAsync(request.Ids ?? [], ct)
                            .ConfigureAwait(false);

                        return true;
                    },
                    _ => Results.NoContent()
                )
        );
        group.MapPost(
            "/categories/{id:int}/rooms",
            (int id, NavigatorPublicRoomRequest request, HttpContext http, CancellationToken ct) =>
                ChangeAsync(
                    http,
                    $"room add {id} {request.RoomId ?? 0} {(request.First ?? true ? "first" : "last")}",
                    async () =>
                        await editor
                            .AddRoomAsync(
                                id,
                                new RoomId(request.RoomId ?? 0),
                                request.First ?? true,
                                AdminIdentity.Of(http).PlayerId,
                                ct
                            )
                            .ConfigureAwait(false),
                    added =>
                        added
                            ? Results.NoContent()
                            : AdminResults.Error(
                                StatusCodes.Status409Conflict,
                                "The room is in this category already."
                            )
                )
        );
        group.MapPut(
            "/categories/{id:int}/rooms/order",
            (int id, NavigatorPublicOrderRequest request, HttpContext http, CancellationToken ct) =>
                ChangeAsync(
                    http,
                    $"room order {id} {Ids(request)}",
                    async () =>
                    {
                        await editor
                            .OrderRoomsAsync(
                                id,
                                [.. (request.Ids ?? []).Select(x => new RoomId(x))],
                                ct
                            )
                            .ConfigureAwait(false);

                        return true;
                    },
                    _ => Results.NoContent()
                )
        );
        group.MapPost(
            "/categories/{id:int}/rooms/{roomId:int}/move",
            (
                int id,
                int roomId,
                NavigatorPublicMoveRequest request,
                HttpContext http,
                CancellationToken ct
            ) =>
                ChangeAsync(
                    http,
                    $"room move {id} {roomId} {request.ToCategoryId ?? 0} {request.Index ?? 0}",
                    async () =>
                    {
                        await editor
                            .MoveRoomAsync(
                                id,
                                new RoomId(roomId),
                                request.ToCategoryId ?? 0,
                                request.Index,
                                ct
                            )
                            .ConfigureAwait(false);

                        return true;
                    },
                    _ => Results.NoContent()
                )
        );
        group.MapDelete(
            "/categories/{id:int}/rooms/{roomId:int}",
            (int id, int roomId, HttpContext http, CancellationToken ct) =>
                ChangeAsync(
                    http,
                    $"room remove {id} {roomId}",
                    async () =>
                        await editor
                            .RemoveRoomAsync(id, new RoomId(roomId), ct)
                            .ConfigureAwait(false),
                    removed => removed ? Results.NoContent() : Results.NotFound()
                )
        );
    }

    private async Task<IResult> ListAsync(CancellationToken ct) =>
        Results.Ok(
            new NavigatorPublicResponse([
                .. (await editor.ListAsync(ct).ConfigureAwait(false)).Select(
                    x => new NavigatorPublicCategoryItem(
                        x.Id,
                        x.Name,
                        x.OrderNum,
                        x.ForceClosed,
                        (int)x.ViewMode,
                        x.Visible,
                        x.Kind == NavigatorPublicCategoryKind.StaffPicks
                            ? KIND_STAFF_PICKS
                            : KIND_MANUAL,
                        x.PluginKey,
                        x.SearchCode,
                        [
                            .. x.Rooms.Select(r => new NavigatorPublicRoomItem(
                                r.RoomId.Value,
                                r.Name,
                                r.OwnerId.Value,
                                r.OwnerName,
                                r.AddedAt,
                                r.AddedBy?.Value
                            )),
                        ]
                    )
                ),
            ])
        );

    private Task<IResult> SaveCategoryAsync(
        int id,
        NavigatorPublicCategoryRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ChangeAsync(
            http,
            $"category {(id == 0 ? "add" : $"save {id}")} {request.Name}",
            async () =>
                await editor
                    .SaveCategoryAsync(
                        id,
                        new PublicCategoryChange(
                            request.Name,
                            request.Visible,
                            request.ForceClosed,
                            request.ViewMode is { } viewMode
                                ? (NavigatorViewModeType)viewMode
                                : null
                        ),
                        request.Kind switch
                        {
                            null or KIND_MANUAL => NavigatorPublicCategoryKind.Manual,
                            KIND_STAFF_PICKS => NavigatorPublicCategoryKind.StaffPicks,
                            _ => throw new ArgumentException(
                                $"A category's kind is {KIND_MANUAL} or {KIND_STAFF_PICKS}."
                            ),
                        },
                        null,
                        ct
                    )
                    .ConfigureAwait(false),
            savedId => Results.Ok(new { id = savedId })
        );

    /// <summary>A change, in the command log whatever came of it: what it answers, or 400 with why it was refused.</summary>
    private async Task<IResult> ChangeAsync<T>(
        HttpContext http,
        string arguments,
        Func<Task<T>> change,
        Func<T, IResult> answer
    )
    {
        var staff = AdminIdentity.Of(http).PlayerId;

        try
        {
            var result = await change().ConfigureAwait(false);

            await LogAsync(staff, arguments, CommandOutcome.Completed).ConfigureAwait(false);

            return answer(result);
        }
        catch (ArgumentException ex)
        {
            await LogAsync(staff, arguments, CommandOutcome.Refused).ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private Task LogAsync(PlayerId staff, string arguments, CommandOutcome outcome) =>
        actions.WriteAsync(
            AdminActionLog.NAVIGATOR,
            staff,
            $"public {arguments}".TrimEnd(),
            outcome,
            CancellationToken.None
        );

    private static string Ids(NavigatorPublicOrderRequest request) =>
        string.Join(',', request.Ids ?? []);
}
