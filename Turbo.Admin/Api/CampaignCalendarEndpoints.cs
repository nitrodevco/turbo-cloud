using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Campaigns;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The campaign calendars, for staff with <c>admin.campaigns.manage</c>: list, read, save and
/// remove them with each door's gift. See <c>docs/campaign-calendar.md</c>.
/// </summary>
internal sealed class CampaignCalendarEndpoints(
    ICampaignCalendarService calendars,
    ILogger<CampaignCalendarEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't manage the campaign calendars.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/campaign-calendars")
            .RequirePermission(PermissionNodes.Admin.CAMPAIGNS_MANAGE, NO_ACCESS)
            .ViewIsEnough(
                "Campaign calendars have one node, admin.campaigns.manage, to see and change them."
            );

        group.MapGet(
            "/",
            async (CancellationToken ct) =>
                Results.Ok(new { calendars = await calendars.ListAsync(ct).ConfigureAwait(false) })
        );
        group.MapGet(
            "/{id:int}",
            async (int id, CancellationToken ct) =>
                await calendars.GetAsync(id, ct).ConfigureAwait(false) is { } calendar
                    ? Results.Ok(calendar)
                    : Results.NotFound()
        );
        group.MapPost(
            "/",
            (CampaignCalendarDefinition calendar, HttpContext http, CancellationToken ct) =>
                SaveAsync(0, calendar, http, ct)
        );
        group.MapPut("/{id:int}", SaveAsync);
        group.MapDelete(
            "/{id:int}",
            async (int id, HttpContext http, CancellationToken ct) =>
            {
                if (!await calendars.DeleteAsync(id, ct).ConfigureAwait(false))
                    return Results.NotFound();

                logger.LogInformation(
                    "Staff {StaffId} removed campaign calendar {CalendarId}",
                    AdminIdentity.Of(http).PlayerId.Value,
                    id
                );

                return Results.NoContent();
            }
        );
    }

    private async Task<IResult> SaveAsync(
        int id,
        CampaignCalendarDefinition calendar,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            var saved = await calendars
                .SaveAsync(calendar with { Id = id }, ct)
                .ConfigureAwait(false);

            logger.LogInformation(
                "Staff {StaffId} saved campaign calendar {CalendarId} ({Name})",
                AdminIdentity.Of(http).PlayerId.Value,
                saved.Id,
                saved.Name
            );

            return Results.Ok(saved);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }
}
