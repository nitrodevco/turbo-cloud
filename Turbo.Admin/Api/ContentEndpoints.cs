using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The game's content: achievements and badges. For staff with <c>admin.content.view</c>;
/// changing anything needs <c>content.manage</c> as well.
/// <para>
/// An achievement is published through its catalog (<see cref="IAchievementCatalog.ImportAsync"/>):
/// a new revision, checked as the catalog checks every definition, on record with who published
/// it and why, and live at once. A badge is given and taken through the player's badge grain.
/// </para>
/// </summary>
internal sealed partial class ContentEndpoints(
    IGrainFactory grainFactory,
    IAchievementCatalog achievements,
    AdminBadgeQueries badges,
    ILogger<ContentEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the game's content.";
    private const string NO_MANAGE = "You can't change the game's content.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/content").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/achievements", ListAchievementsAsync);
        group.MapGet("/achievements/{id:int}", GetAchievement);
        group.MapPost("/achievements/check", CheckAchievementAsync);
        group.MapPut("/achievements/{id:int}", PublishAchievementAsync);
        group.MapGet(
            "/badges",
            async (string? q, CancellationToken ct) =>
                Results.Ok(new { badges = await badges.SearchAsync(q, ct).ConfigureAwait(false) })
        );
        group.MapGet(
            "/badges/{code}/holders",
            async (string code, CancellationToken ct) =>
                Results.Ok(
                    new { holders = await badges.HoldersAsync(code, ct).ConfigureAwait(false) }
                )
        );
        group.MapPut("/badges/{code}/rarity", SetRarityAsync);
        group.MapPost("/badges/{code}/holders", GiveBadgeAsync);
        group.MapDelete("/badges/{code}/holders/{playerId:int}", TakeBadgeAsync);
    }

    private async Task<IResult> ListAchievementsAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            new
            {
                achievements = achievements
                    .Current.OrderBy(x => x.Category)
                    .ThenBy(x => x.Order)
                    .ThenBy(x => x.Id)
                    .Select(x => new AchievementItem(
                        x.Id,
                        x.Key,
                        x.Revision,
                        x.Category,
                        x.SubCategory,
                        x.Order,
                        x.State,
                        x.Source,
                        x.Levels.Length,
                        x.Levels.Length > 0 ? x.Levels[0].BadgeCode : null,
                        x.Levels.Length > 0 ? x.Levels[^1].BadgeCode : null
                    )),
                canManage = await CanManageAsync(http, ct).ConfigureAwait(false),
            }
        );

    private IResult GetAchievement(int id) =>
        achievements.Current.FirstOrDefault(x => x.Id == id) is { } definition
            ? Results.Ok(
                new
                {
                    definitionJson = JsonSerializer.Serialize(
                        definition,
                        new JsonSerializerOptions { WriteIndented = true }
                    ),
                }
            )
            : Results.NotFound();

    /// <summary>Checks a definition as publishing it would, and saves nothing.</summary>
    private Task<IResult> CheckAchievementAsync(
        AchievementPublishRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var definition = Next(Read(request.DefinitionJson));

                await achievements
                    .ImportAsync(
                        [definition],
                        apply: false,
                        Actor(http),
                        "check",
                        $"panel-check-{Guid.NewGuid():N}",
                        ct
                    )
                    .ConfigureAwait(false);

                return Results.Ok(new { revision = definition.Revision });
            }
        );

    private Task<IResult> PublishAchievementAsync(
        int id,
        AchievementPublishRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var definition = Next(Read(request.DefinitionJson));
                var reason = (request.Reason ?? string.Empty).Trim();

                if (definition.Id != id)
                    throw new ArgumentException("The definition is of another achievement.");

                if (reason.Length == 0)
                    throw new ArgumentException("Say why: a publish is on record with its reason.");

                await achievements
                    .ImportAsync(
                        [definition],
                        apply: true,
                        Actor(http),
                        reason,
                        $"panel-{Guid.NewGuid():N}",
                        ct
                    )
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "{Actor} published achievement {Key} revision {Revision}: {Reason}",
                    Actor(http),
                    definition.Key,
                    definition.Revision,
                    reason
                );

                return Results.Ok(new { revision = definition.Revision });
            }
        );

    private Task<IResult> SetRarityAsync(
        string code,
        BadgeRarityRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                await badges
                    .SetRarityAsync(CheckCode(code), request.Rarity, ct)
                    .ConfigureAwait(false);

                return Results.NoContent();
            }
        );

    private Task<IResult> GiveBadgeAsync(
        string code,
        BadgeGiveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var player = new PlayerId(request.PlayerId ?? 0);
                var given = await grainFactory
                    .GetPlayerBadgeGrain(player)
                    .GiveBadgeAsync(CheckCode(code), ct)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "{Actor} gave badge {Code} to player {PlayerId}: {Given}",
                    Actor(http),
                    code,
                    player.Value,
                    given
                );

                return given
                    ? Results.NoContent()
                    : AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        "Not given: the player has it already, or there is no such player."
                    );
            }
        );

    private Task<IResult> TakeBadgeAsync(
        string code,
        int playerId,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var taken = await grainFactory
                    .GetPlayerBadgeGrain(new PlayerId(playerId))
                    .RemoveBadgeAsync(CheckCode(code), ct)
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "{Actor} took badge {Code} from player {PlayerId}: {Taken}",
                    Actor(http),
                    code,
                    playerId,
                    taken
                );

                return taken ? Results.NoContent() : Results.NotFound();
            }
        );

    /// <summary>A definition as the catalog reads it; refused when it isn't one.</summary>
    private static AchievementDefinition Read(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<AchievementDefinition>(json ?? string.Empty)
                ?? throw new InvalidOperationException("It is empty.");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new ArgumentException($"That isn't an achievement definition. {ex.Message}");
        }
    }

    /// <summary>The definition as the next revision of its achievement: published over what there is.</summary>
    private AchievementDefinition Next(AchievementDefinition definition) =>
        definition with
        {
            Revision =
                (achievements.Current.FirstOrDefault(x => x.Id == definition.Id)?.Revision ?? 0)
                + 1,
        };

    private static string CheckCode(string code)
    {
        if (!BadgeCodePattern().IsMatch(code))
            throw new ArgumentException("A badge code is letters, digits and _, 64 at most.");

        return code;
    }

    private static string Actor(HttpContext http) => $"panel:{AdminIdentity.Of(http).Name}";

    /// <summary>
    /// A change by someone who holds <c>content.manage</c>: what it answers, or 400 with why the
    /// game refused it.
    /// </summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    )
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Content.MANAGE,
            ct
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
                PermissionNodes.Admin.CONTENT_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }

    [GeneratedRegex("^[A-Za-z0-9_]{1,64}$")]
    private static partial Regex BadgeCodePattern();
}
