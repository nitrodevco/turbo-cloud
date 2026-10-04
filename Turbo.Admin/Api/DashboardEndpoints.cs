using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Orleans.Runtime;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Availability;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;

namespace Turbo.Admin.Api;

/// <summary>The figures <c>:status</c> reports, and the busiest rooms, for the panel's first page.</summary>
internal sealed class DashboardEndpoints(
    IGrainFactory grainFactory,
    ISessionGateway sessionGateway,
    IHotelAvailability availability
)
{
    private const int BUSIEST_ROOMS = 20;

    public void Map(RouteGroupBuilder secured) => secured.MapGet("/dashboard", GetAsync);

    private async Task<IResult> GetAsync(CancellationToken ct)
    {
        using var process = Process.GetCurrentProcess();

        // Every active room with its live population, from the directory's own listing view.
        var listing = await grainFactory
            .GetRoomDirectoryGrain()
            .GetListingViewAsync(Guid.Empty, 0, includeActiveRooms: true, ct)
            .ConfigureAwait(false);
        var hosts = await grainFactory
            .GetGrain<IManagementGrain>(0)
            .GetHosts(false)
            .ConfigureAwait(false);
        var current = availability.Current;

        return Results.Ok(
            new DashboardResponse(
                Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown",
                process.StartTime.ToUniversalTime(),
                sessionGateway.GetOnlinePlayerIds().Count,
                listing.ActiveRooms.Length,
                hosts.Count(x => x.Value == SiloStatus.Active),
                hosts.Count,
                process.WorkingSet64 / 1024 / 1024,
                GC.GetTotalMemory(false) / 1024 / 1024,
                current.Phase.ToString(),
                current.AtUtc,
                [
                    .. listing
                        .ActiveRooms.Where(x => x.Population > 0)
                        .OrderByDescending(x => x.Population)
                        .Take(BUSIEST_ROOMS)
                        .Select(x => new DashboardRoom(
                            x.RoomId.Value,
                            x.Name,
                            x.OwnerName,
                            x.Population,
                            x.PlayersMax
                        )),
                ]
            )
        );
    }
}
