using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;

namespace Turbo.Admin.Api;

/// <summary>
/// The daily tasks and the quests (<c>/quests</c>), for staff with <c>admin.content.view</c>;
/// changing them needs <c>content.manage</c> as well.
/// </summary>
internal sealed class QuestEndpoints(ContentAccess access, AdminQuestEditor quests)
{
    public void Map(RouteGroupBuilder secured)
    {
        var group = ContentAccess.Require(secured.MapGroup("/quests"));

        group.MapGet(
            "/daily-tasks",
            async (HttpContext http, CancellationToken ct) =>
                Results.Ok(
                    await quests
                        .ListDailyTasksAsync(
                            await access.CanManageAsync(http, ct).ConfigureAwait(false),
                            ct
                        )
                        .ConfigureAwait(false)
                )
        );
        group.MapPost(
            "/daily-tasks",
            (DailyTaskRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Saved(
                        await quests
                            .SaveDailyTaskAsync(0, request, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapPut(
            "/daily-tasks/{id:int}",
            (int id, DailyTaskRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Saved(
                        await quests
                            .SaveDailyTaskAsync(id, request, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapDelete(
            "/daily-tasks/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Removed(
                        await quests
                            .RemoveDailyTaskAsync(id, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );

        group.MapGet(
            "/",
            async (HttpContext http, CancellationToken ct) =>
                Results.Ok(
                    await quests
                        .ListQuestsAsync(
                            await access.CanManageAsync(http, ct).ConfigureAwait(false),
                            ct
                        )
                        .ConfigureAwait(false)
                )
        );
        group.MapPost(
            "/campaigns",
            (QuestCampaignRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Saved(
                        await quests
                            .SaveCampaignAsync(0, request, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapPut(
            "/campaigns/{id:int}",
            (int id, QuestCampaignRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Saved(
                        await quests
                            .SaveCampaignAsync(id, request, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapDelete(
            "/campaigns/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Removed(
                        await quests
                            .RemoveCampaignAsync(id, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapPost(
            "/quests",
            (QuestRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Saved(
                        await quests
                            .SaveQuestAsync(0, request, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapPut(
            "/quests/{id:int}",
            (int id, QuestRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Saved(
                        await quests
                            .SaveQuestAsync(id, request, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
        group.MapDelete(
            "/quests/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Removed(
                        await quests
                            .RemoveQuestAsync(id, ContentAccess.Actor(http), ct)
                            .ConfigureAwait(false)
                    )
                )
        );
    }

    private static IResult Saved(int id) => Results.Ok(new { id });

    private static IResult Removed(bool removed) =>
        removed ? Results.NoContent() : Results.NotFound();
}
