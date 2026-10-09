using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// What the reception's widgets show besides its variables (those are under
/// <see cref="GamedataEndpoints"/>): the promo articles, the community goals and the bonus rare campaigns. For staff with <c>admin.gamedata.view</c>,
/// as the hotel view is; changing them needs <c>gamedata.manage</c>.
/// </summary>
internal sealed class ReceptionEndpoints(
    IGrainFactory grainFactory,
    IPromoArticleService articles,
    ICommunityGoalService goals,
    IBonusRareService bonusRare,
    ILogger<ReceptionEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the hotel view.";
    private const string NO_MANAGE = "You can't change the hotel view.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/hotel-view").AddEndpointFilter(RequireViewAsync);

        group.MapGet(
            "/articles",
            async (CancellationToken ct) =>
                Results.Ok(new { articles = await articles.ListAsync(ct).ConfigureAwait(false) })
        );
        group.MapPost(
            "/articles",
            (PromoArticleRequest request, HttpContext http, CancellationToken ct) =>
                SaveArticleAsync(0, request, http, ct)
        );
        group.MapPut("/articles/{id:int}", SaveArticleAsync);
        group.MapDelete("/articles/{id:int}", DeleteArticleAsync);
        group.MapPut("/articles/order", ReorderArticlesAsync);
        group.MapGet(
            "/goals",
            async (CancellationToken ct) =>
                Results.Ok(new { goals = await goals.ListAsync(ct).ConfigureAwait(false) })
        );
        group.MapGet(
            "/goals/{id:int}/standing",
            async (int id, CancellationToken ct) =>
                await goals.GetStandingAsync(id, ct).ConfigureAwait(false) is { } standing
                    ? Results.Ok(standing)
                    : Results.NotFound()
        );
        group.MapPost(
            "/goals",
            (CommunityGoalRequest request, HttpContext http, CancellationToken ct) =>
                SaveGoalAsync(0, request, http, ct)
        );
        group.MapPut("/goals/{id:int}", SaveGoalAsync);
        group.MapDelete("/goals/{id:int}", DeleteGoalAsync);
        group.MapGet(
            "/bonus-rare",
            async (CancellationToken ct) =>
                Results.Ok(new { campaigns = await bonusRare.ListAsync(ct).ConfigureAwait(false) })
        );
        group.MapGet(
            "/bonus-rare/standing",
            async (string? code, CancellationToken ct) =>
                Results.Ok(
                    await bonusRare.GetStandingAsync(code ?? string.Empty, ct).ConfigureAwait(false)
                )
        );
        group.MapPost(
            "/bonus-rare",
            (BonusRareCampaignRequest request, HttpContext http, CancellationToken ct) =>
                SaveCampaignAsync(0, request, http, ct)
        );
        group.MapPut("/bonus-rare/{id:int}", SaveCampaignAsync);
        group.MapDelete("/bonus-rare/{id:int}", DeleteCampaignAsync);
        group.MapPost("/bonus-rare/purchases", RecordPurchaseAsync);
    }

    private Task<IResult> SaveArticleAsync(
        int id,
        PromoArticleRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                Results.Ok(
                    await articles
                        .SaveAsync(
                            new PromoArticleSnapshot
                            {
                                Id = id,
                                Title = request.Title ?? string.Empty,
                                BodyText = request.BodyText ?? string.Empty,
                                ButtonText = request.ButtonText ?? string.Empty,
                                LinkType = request.LinkType ?? PromoArticleLinkType.None,
                                LinkContent = request.LinkContent ?? string.Empty,
                                ImageUrl = request.ImageUrl ?? string.Empty,
                                SortOrder = 0,
                                Visible = request.Visible ?? true,
                                StartsAt = Utc(request.StartsAt),
                                EndsAt = Utc(request.EndsAt),
                            },
                            ct
                        )
                        .ConfigureAwait(false)
                )
        );

    private Task<IResult> DeleteArticleAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await articles.DeleteAsync(id, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    private Task<IResult> ReorderArticlesAsync(
        ReorderRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                await articles.ReorderAsync(request.Ids ?? [], ct).ConfigureAwait(false);

                return Results.NoContent();
            }
        );

    private Task<IResult> SaveGoalAsync(
        int id,
        CommunityGoalRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                Results.Ok(
                    await goals
                        .SaveAsync(
                            new CommunityGoalSnapshot
                            {
                                Id = id,
                                Code = request.Code ?? string.Empty,
                                Mode = request.Mode ?? CommunityGoalMode.Normal,
                                StartsAt = Utc(request.StartsAt) ?? DateTime.MinValue,
                                EndsAt = Utc(request.EndsAt) ?? DateTime.MinValue,
                                LevelScores = [.. request.LevelScores ?? []],
                                RewardRanks = [.. request.RewardRanks ?? []],
                                SideOnePageId = request.SideOnePageId,
                                SideTwoPageId = request.SideTwoPageId,
                            },
                            ct
                        )
                        .ConfigureAwait(false)
                )
        );

    private Task<IResult> DeleteGoalAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await goals.DeleteAsync(id, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    private Task<IResult> SaveCampaignAsync(
        int id,
        BonusRareCampaignRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                Results.Ok(
                    await bonusRare
                        .SaveAsync(
                            new BonusRareCampaignSnapshot
                            {
                                Id = id,
                                Code = request.Code ?? string.Empty,
                                FurnitureName = request.FurnitureName ?? string.Empty,
                                ProductCode = request.ProductCode ?? string.Empty,
                                CreditsRequired = request.CreditsRequired ?? 0,
                                Source = request.Source ?? BonusRareSource.PurchasedCredits,
                                StartsAt = Utc(request.StartsAt) ?? DateTime.UtcNow,
                                EndsAt = Utc(request.EndsAt),
                            },
                            ct
                        )
                        .ConfigureAwait(false)
                )
        );

    private Task<IResult> DeleteCampaignAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await bonusRare.DeleteAsync(id, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : Results.NotFound()
        );

    /// <summary>
    /// Credits a player bought, recorded by staff for the bonus rare under the purchase's
    /// reference, which counts once.
    /// </summary>
    private Task<IResult> RecordPurchaseAsync(
        BonusRarePurchaseRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                var result = await bonusRare
                    .RecordPurchaseAsync(
                        new PlayerId(request.PlayerId ?? 0),
                        request.Credits ?? 0,
                        request.Reference ?? string.Empty,
                        ct
                    )
                    .ConfigureAwait(false);

                logger.LogInformation(
                    "Staff {StaffId} recorded {Credits} bought credits for player {PlayerId} ({Reference}): {Result}",
                    AdminIdentity.Of(http).PlayerId.Value,
                    request.Credits,
                    request.PlayerId,
                    request.Reference,
                    result
                );

                return Results.Ok(new { result });
            }
        );

    /// <summary>A time the panel sent, as UTC: the panel sends UTC, marked or not.</summary>
    private static DateTime? Utc(DateTime? time) =>
        time is { } t
            ? t.Kind == DateTimeKind.Local
                ? t.ToUniversalTime()
                : DateTime.SpecifyKind(t, DateTimeKind.Utc)
            : null;

    /// <summary>
    /// A change by someone who holds <c>gamedata.manage</c>: what it answers, or 400 with why it
    /// was refused.
    /// </summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    )
    {
        if (
            !await grainFactory
                .HasPermissionAsync(
                    AdminIdentity.Of(http).PlayerId,
                    PermissionNodes.Gamedata.MANAGE,
                    ct
                )
                .ConfigureAwait(false)
        )
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.GAMEDATA_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
