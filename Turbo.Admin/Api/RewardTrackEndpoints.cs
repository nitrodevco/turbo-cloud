using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.RewardTracks;

namespace Turbo.Admin.Api;

/// <summary>
/// The reward tracks of the Progression menu, for staff with <c>admin.rewardtracks.manage</c>:
/// list, read, save and remove them, take Habbo's in from a capture (a plan first, then the
/// import), and read them again from the database. See <c>docs/reward-tracks.md</c>.
/// </summary>
internal sealed class RewardTrackEndpoints(AdminRewardTracks tracks)
{
    private const string NO_ACCESS = "You can't manage the reward tracks.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/reward-tracks")
            .RequirePermission(PermissionNodes.Admin.REWARD_TRACKS_MANAGE, NO_ACCESS)
            .ViewIsEnough(
                "Reward tracks have one node, admin.rewardtracks.manage, to see and change them."
            );

        group.MapGet(
            "/",
            () =>
                Results.Ok(new { tracks = tracks.List(), actionTypes = RewardTrackActionTypes.ALL })
        );
        group.MapGet(
            "/{id}",
            (string id) =>
                tracks.Get(id) is { } track
                    ? Results.Ok(
                        new
                        {
                            track,
                            warnings = tracks.Warnings(track),
                            furni = tracks.PrizeFurni(track),
                        }
                    )
                    : Results.NotFound()
        );
        group.MapPut("/{id}", SaveAsync);
        group.MapDelete(
            "/{id}",
            async (string id, HttpContext http, CancellationToken ct) =>
                await tracks.DeleteAsync(AdminIdentity.Of(http).PlayerId, id, ct)
                    ? Results.NoContent()
                    : Results.NotFound()
        );
        group.MapPost(
            "/import/preview",
            (RewardTrackImportRequest request) => Refusing(() => Results.Ok(tracks.Plan(request)))
        );
        group.MapPost(
            "/import",
            (RewardTrackImportRequest request, HttpContext http, CancellationToken ct) =>
                RefusingAsync(async () =>
                    Results.Ok(
                        await tracks.ImportAsync(AdminIdentity.Of(http).PlayerId, request, ct)
                    )
                )
        );
        group.MapPost(
            "/reload",
            async (HttpContext http, CancellationToken ct) =>
            {
                await tracks.ReloadAsync(AdminIdentity.Of(http).PlayerId, ct);

                return Results.Ok(new { tracks = tracks.List() });
            }
        );
    }

    private Task<IResult> SaveAsync(
        string id,
        RewardTrackDefinition track,
        HttpContext http,
        CancellationToken ct
    ) =>
        RefusingAsync(async () =>
        {
            track.Id = id;

            var saved = await tracks.SaveAsync(AdminIdentity.Of(http).PlayerId, track, ct);

            return Results.Ok(
                new
                {
                    track = saved,
                    warnings = tracks.Warnings(saved),
                    furni = tracks.PrizeFurni(saved),
                }
            );
        });

    private static IResult Refusing(Func<IResult> change)
    {
        try
        {
            return change();
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private static async Task<IResult> RefusingAsync(Func<Task<IResult>> change)
    {
        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }
}
