using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Sound;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Admin.Api;

/// <summary>
/// The trax songs song disks play: listing them, with how many disks carry each, for staff with
/// <c>admin.catalog.view</c>, since the catalog sells them; adding, changing and removing them,
/// for those who also hold <c>catalog.manage</c>. The songs are the song directory grain's, which
/// checks every change; a song a disk carries cannot be removed.
/// </summary>
internal sealed class SongEndpoints(IGrainFactory grainFactory)
{
    private const string NO_ACCESS = "You can't see the songs.";
    private const string NO_MANAGE = "You can't change the songs.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/songs").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", ListAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:int}", UpdateAsync);
        group.MapDelete("/{id:int}", DeleteAsync);
    }

    private ISongDirectoryGrain Directory => grainFactory.GetSongDirectoryGrain();

    private async Task<IResult> ListAsync(CancellationToken ct)
    {
        var songs = await Directory.GetAllSongsAsync(ct).ConfigureAwait(false);
        var disks = await Directory.CountDisksAsync(ct).ConfigureAwait(false);

        return Results.Ok(new SongsResponse([.. songs.Select(x => Item(x, disks))]));
    }

    private async Task<IResult> GetAsync(int id, CancellationToken ct)
    {
        var songs = await Directory.GetSongsAsync([id], ct).ConfigureAwait(false);

        if (songs.IsDefaultOrEmpty)
            return AdminResults.Error(StatusCodes.Status404NotFound, "That song is gone.");

        return Results.Ok(
            Detail(songs[0], await Directory.CountDisksAsync(ct).ConfigureAwait(false))
        );
    }

    private Task<IResult> CreateAsync(
        SongRequest request,
        HttpContext http,
        CancellationToken ct
    ) => ManageAsync(http, ct, () => Directory.CreateSongAsync(Draft(request), ct));

    private Task<IResult> UpdateAsync(
        int id,
        SongRequest request,
        HttpContext http,
        CancellationToken ct
    ) => ManageAsync(http, ct, () => Directory.UpdateSongAsync(id, Draft(request), ct));

    private Task<IResult> DeleteAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(http, ct, () => Directory.DeleteSongAsync(id, ct));

    /// <summary>
    /// A change by someone who holds <c>catalog.manage</c>: the song as saved (no content after a
    /// delete), 404 for a song that is gone, or 400 with why it was refused.
    /// </summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<SongEditResult>> edit
    )
    {
        if (
            !await grainFactory
                .HasPermissionAsync(
                    AdminIdentity.Of(http).PlayerId,
                    PermissionNodes.Catalog.MANAGE,
                    ct
                )
                .ConfigureAwait(false)
        )
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        var result = await edit().ConfigureAwait(false);

        if (!result.Saved)
            return AdminResults.Error(
                result.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
                result.Error ?? "Not saved."
            );

        if (result.Song is not { } song)
            return Results.NoContent();

        return Results.Ok(Detail(song, await Directory.CountDisksAsync(ct).ConfigureAwait(false)));
    }

    private static SongDraft Draft(SongRequest request) =>
        new()
        {
            Name = request.Name ?? string.Empty,
            Author = request.Author ?? string.Empty,
            Track = request.Track ?? string.Empty,
            LengthSeconds = request.Length,
            IsOfficial = request.Official,
            Code = request.Code,
        };

    private static SongItem Item(SongSnapshot song, ImmutableDictionary<int, int> disks) =>
        new(
            song.Id,
            song.Name,
            song.Author,
            song.LengthSeconds,
            song.IsOfficial,
            disks.GetValueOrDefault(song.Id),
            song.Code.Length > 0 ? song.Code : null
        );

    private static SongDetail Detail(SongSnapshot song, ImmutableDictionary<int, int> disks) =>
        new(
            song.Id,
            song.Name,
            song.Author,
            song.LengthSeconds,
            song.IsOfficial,
            disks.GetValueOrDefault(song.Id),
            song.Code.Length > 0 ? song.Code : null,
            song.Track
        );

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.CATALOG_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
