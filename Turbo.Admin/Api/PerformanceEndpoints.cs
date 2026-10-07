using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Performance;

namespace Turbo.Admin.Api;

/// <summary>
/// The Performance page: how this server has been running, from <see cref="AdminPerformanceRecorder"/>.
/// Open to everyone who may sign in to the panel, like the dashboard.
/// </summary>
internal sealed class PerformanceEndpoints(AdminPerformanceRecorder recorder)
{
    private const int DEFAULT_HOURS = 1;

    public void Map(RouteGroupBuilder secured) =>
        secured.MapGet(
            "/performance",
            (int? hours) => Results.Ok(recorder.Read(hours ?? DEFAULT_HOURS))
        );
}
