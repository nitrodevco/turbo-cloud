using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Turbo.Admin.Api;

/// <summary>Asking after work that outlasted its request (<see cref="AdminJobs"/>).</summary>
internal sealed class JobEndpoints(AdminJobs jobs)
{
    public void Map(RouteGroupBuilder secured)
    {
        secured
            .MapGet("/jobs/{id:guid}", Get)
            .PanelOnly(
                "A job answers only the staff member who started it, whose access its own route checked."
            );
    }

    private IResult Get(Guid id, HttpContext http) =>
        jobs.Get(id, AdminIdentity.Of(http).PlayerId) is { } job
            ? Results.Ok(job)
            : AdminResults.Error(StatusCodes.Status404NotFound, "That job is gone.");
}
