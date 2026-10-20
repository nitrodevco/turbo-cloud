using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Admin.Content;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The game's content: achievements, badges, the navigator's categories, groups, pets, bots, currencies and chat styles. For staff with <c>admin.content.view</c>;
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
    AdminNavigatorEditor navigator,
    AdminGroupQueries groups,
    AdminPetBotEditor petsAndBots,
    AdminCurrencyEditor currencies,
    IChatStyleService chatStyles,
    AdminActionLog actions,
    ILogger<ContentEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the game's content.";
    private const string NO_MANAGE = "You can't change the game's content.";
    private const string NO_GROUP_DELETE = "You can't delete groups: that needs guild.delete_any.";

    // The chat bubbles' bundle is a few hundred kilobytes; this leaves room for a hotel's own.
    private const long CHAT_BUNDLE_MAX_BYTES = 32L * 1024 * 1024;

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
        group.MapGet(
            "/groups",
            async (string? q, int? page, CancellationToken ct) =>
                Results.Ok(await groups.SearchAsync(q, page ?? 0, ct).ConfigureAwait(false))
        );
        group.MapGet(
            "/groups/editor",
            async (CancellationToken ct) =>
                Results.Ok(await groups.GetEditorAsync(ct).ConfigureAwait(false))
        );
        group.MapGet(
            "/groups/{id:int}",
            async (int id, CancellationToken ct) =>
                await groups.GetAsync(id, ct).ConfigureAwait(false) is { } detail
                    ? Results.Ok(detail)
                    : Results.NotFound()
        );
        group.MapPut(
            "/groups/{id:int}",
            (int id, GroupRenameRequest request, HttpContext http, CancellationToken ct) =>
                GroupAsync(
                    id,
                    http,
                    ct,
                    (grain, staff) =>
                        grain.StaffRenameAsync(
                            staff,
                            request.Name ?? string.Empty,
                            request.Description ?? string.Empty,
                            ct
                        ),
                    "Not renamed: a group needs a name."
                )
        );
        group.MapPost(
            "/groups/{id:int}/reset-badge",
            (int id, HttpContext http, CancellationToken ct) =>
                GroupAsync(
                    id,
                    http,
                    ct,
                    (grain, staff) => grain.StaffResetBadgeAsync(staff, ct),
                    "The badge could not be reset."
                )
        );
        group.MapDelete(
            "/groups/{id:int}/members/{playerId:int}",
            (int id, int playerId, HttpContext http, CancellationToken ct) =>
                GroupAsync(
                    id,
                    http,
                    ct,
                    (grain, staff) =>
                        grain.StaffRemoveMemberAsync(staff, new PlayerId(playerId), ct),
                    "Not removed: they must be in the group, and the owner stays until it is deleted."
                )
        );
        group.MapDelete("/groups/{id:int}", DeleteGroupAsync);
        group.MapPost(
            "/groups/parts",
            (GroupBadgePartRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(await groups.SavePartAsync(0, request, ct).ConfigureAwait(false))
                )
        );
        group.MapPut(
            "/groups/parts/{id:int}",
            (int id, GroupBadgePartRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(await groups.SavePartAsync(id, request, ct).ConfigureAwait(false))
                )
        );
        group.MapPost(
            "/groups/colors",
            (GroupColorRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(await groups.SaveColorAsync(0, request, ct).ConfigureAwait(false))
                )
        );
        group.MapPut(
            "/groups/colors/{id:int}",
            (int id, GroupColorRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(await groups.SaveColorAsync(id, request, ct).ConfigureAwait(false))
                )
        );
        group.MapGet(
            "/currencies",
            async (CancellationToken ct) =>
                Results.Ok(await currencies.ListAsync(ct).ConfigureAwait(false))
        );
        group.MapPost(
            "/currencies",
            (CurrencyRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await currencies
                                .SaveAsync(0, request, Actor(http), ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/currencies/{id:int}",
            (int id, CurrencyRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await currencies
                                .SaveAsync(id, request, Actor(http), ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapDelete(
            "/currencies/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Removed(
                            await currencies.DeleteAsync(id, Actor(http), ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapGet(
            "/pets",
            async (CancellationToken ct) =>
                Results.Ok(await petsAndBots.GetPetsAsync(ct).ConfigureAwait(false))
        );
        group.MapPost(
            "/pets/breeds",
            (PetBreedRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await petsAndBots.SaveBreedAsync(0, request, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/pets/breeds/{id:int}",
            (int id, PetBreedRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await petsAndBots.SaveBreedAsync(id, request, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapPost(
            "/pets/speech",
            (PetSpeechRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await petsAndBots.SaveSpeechAsync(0, request, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/pets/speech/{id:int}",
            (int id, PetSpeechRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await petsAndBots.SaveSpeechAsync(id, request, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapDelete(
            "/pets/speech/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Removed(await petsAndBots.DeleteSpeechAsync(id, ct).ConfigureAwait(false))
                )
        );
        group.MapGet(
            "/bots",
            async (string? q, int? page, CancellationToken ct) =>
                Results.Ok(
                    await petsAndBots.SearchBotsAsync(q, page ?? 0, ct).ConfigureAwait(false)
                )
        );
        group.MapPut(
            "/bots/{id:int}",
            (int id, BotStaffEditRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                    {
                        await petsAndBots.UpdateBotAsync(id, request, ct).ConfigureAwait(false);

                        return Results.NoContent();
                    }
                )
        );
        group.MapPost(
            "/bots/{id:int}/pickup",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                    {
                        await petsAndBots.PickupBotAsync(id, ct).ConfigureAwait(false);

                        return Results.NoContent();
                    }
                )
        );
        group.MapDelete(
            "/bots/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                    {
                        await petsAndBots.DeleteBotAsync(id, ct).ConfigureAwait(false);

                        return Results.NoContent();
                    }
                )
        );
        group.MapGet("/chat-styles", ListChatStylesAsync);
        group.MapGet(
            "/chat-styles/{id:int}/bitmaps/{file}",
            async (int id, string file, CancellationToken ct) =>
                await chatStyles.GetBitmapAsync(id, file, ct).ConfigureAwait(false) is { } png
                    ? Results.File(png, "image/png")
                    : Results.NotFound()
        );
        group.MapPost(
            "/chat-styles",
            (ChatStyleRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await chatStyles
                                .SaveAsync(0, request.ToEdit(), AdminIdentity.Of(http).PlayerId, ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/chat-styles/{id:int}",
            (int id, ChatStyleRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await chatStyles
                                .SaveAsync(
                                    id,
                                    request.ToEdit(),
                                    AdminIdentity.Of(http).PlayerId,
                                    ct
                                )
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapDelete(
            "/chat-styles/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Removed(
                            await chatStyles
                                .DeleteAsync(id, AdminIdentity.Of(http).PlayerId, ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapPost("/chat-styles/import", ImportChatStylesAsync);
        group.MapPost(
            "/chat-styles/import-client",
            (HttpContext http, CancellationToken ct, bool replace = false) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                    {
                        try
                        {
                            return Results.Ok(
                                await chatStyles
                                    .ImportFromClientAsync(
                                        AdminIdentity.Of(http).PlayerId,
                                        replace,
                                        ct
                                    )
                                    .ConfigureAwait(false)
                            );
                        }
                        catch (InvalidOperationException ex)
                        {
                            return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                        }
                    }
                )
        );
        group.MapGet(
            "/navigator",
            async (CancellationToken ct) =>
                Results.Ok(await navigator.GetAsync(ct).ConfigureAwait(false))
        );
        group.MapPost(
            "/navigator/categories",
            (NavigatorFlatCategoryRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await navigator
                                .SaveFlatCategoryAsync(0, request, ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/navigator/categories/{id:int}",
            (
                int id,
                NavigatorFlatCategoryRequest request,
                HttpContext http,
                CancellationToken ct
            ) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await navigator
                                .SaveFlatCategoryAsync(id, request, ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapDelete(
            "/navigator/categories/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Removed(
                            await navigator.DeleteFlatCategoryAsync(id, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapPost(
            "/navigator/event-categories",
            (NavigatorEventCategoryRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await navigator
                                .SaveEventCategoryAsync(0, request, ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/navigator/event-categories/{id:int}",
            (
                int id,
                NavigatorEventCategoryRequest request,
                HttpContext http,
                CancellationToken ct
            ) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await navigator
                                .SaveEventCategoryAsync(id, request, ct)
                                .ConfigureAwait(false)
                        )
                )
        );
        group.MapDelete(
            "/navigator/event-categories/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Removed(
                            await navigator.DeleteEventCategoryAsync(id, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapPost(
            "/navigator/tabs",
            (NavigatorContextRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await navigator.SaveContextAsync(0, request, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapPut(
            "/navigator/tabs/{id:int}",
            (int id, NavigatorContextRequest request, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Saved(
                            await navigator.SaveContextAsync(id, request, ct).ConfigureAwait(false)
                        )
                )
        );
        group.MapDelete(
            "/navigator/tabs/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ManageAsync(
                    http,
                    ct,
                    async () =>
                        Removed(await navigator.DeleteContextAsync(id, ct).ConfigureAwait(false))
                )
        );
    }

    private async Task<IResult> ListChatStylesAsync(CancellationToken ct)
    {
        var list = await chatStyles.ListAsync(ct).ConfigureAwait(false);

        return Results.Ok(
            new ChatStyleListResponse(
                [.. list.Styles.Select(ChatStyleItem.From)],
                list.NextId,
                list.Bundle is null ? null : AssetBundleResponse.From(list.Bundle),
                list.LibraryBitmaps
            )
        );
    }

    /// <summary>A habbo-free-flow-chat-com bundle sent as a form's <c>file</c>, taken in.</summary>
    private Task<IResult> ImportChatStylesAsync(HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                if (!http.Request.HasFormContentType)
                    return AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        "Send the bundle as a form."
                    );

                if (
                    http.Features.Get<IHttpMaxRequestBodySizeFeature>() is
                    { IsReadOnly: false } limit
                )
                    limit.MaxRequestBodySize = CHAT_BUNDLE_MAX_BYTES;

                var form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);

                if (form.Files.GetFile("file") is not { } file)
                    return AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        "Choose the bundle."
                    );

                using var memory = new MemoryStream(
                    (int)Math.Min(file.Length, CHAT_BUNDLE_MAX_BYTES)
                );
                var stream = file.OpenReadStream();
                await using var streamScope = stream.ConfigureAwait(false);

                await stream.CopyToAsync(memory, ct).ConfigureAwait(false);

                return Results.Ok(
                    await chatStyles
                        .ImportBundleAsync(memory.ToArray(), AdminIdentity.Of(http).PlayerId, ct)
                        .ConfigureAwait(false)
                );
            }
        );

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

    /// <summary>
    /// Deletes a group for staff who hold <c>guild.delete_any</c>, the node that deletes any group
    /// in the game, not <c>content.manage</c>. It is not held to <c>GuildConfig.DeletionEnabled</c>
    /// or the member cap, which keep players from deleting groups in the client: the panel is
    /// where staff remove an abusive group whatever those say. A delete cannot be undone (the
    /// roster and the forum go with it), so every one is in the command log, refusals included.
    /// </summary>
    private async Task<IResult> DeleteGroupAsync(int id, HttpContext http, CancellationToken ct)
    {
        var staff = AdminIdentity.Of(http).PlayerId;
        var arguments = $"delete {id}";

        if (
            !await grainFactory
                .HasPermissionAsync(staff, PermissionNodes.Guild.DELETE_ANY, ct)
                .ConfigureAwait(false)
        )
        {
            await actions
                .WriteAsync(AdminActionLog.GROUP, staff, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_GROUP_DELETE);
        }

        var deleted = await grainFactory
            .GetGuildGrain(new GuildId(id))
            .StaffDeleteAsync(staff, ct)
            .ConfigureAwait(false);

        await actions
            .WriteAsync(
                AdminActionLog.GROUP,
                staff,
                arguments,
                deleted ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return deleted
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status400BadRequest, "There is no such group.");
    }

    /// <summary>A staff change to a group through its grain: no content, or 400 with why not.</summary>
    private Task<IResult> GroupAsync(
        int id,
        HttpContext http,
        CancellationToken ct,
        Func<IGuildGrain, PlayerId, Task<bool>> change,
        string refusal
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await change(
                        grainFactory.GetGuildGrain(new GuildId(id)),
                        AdminIdentity.Of(http).PlayerId
                    )
                    .ConfigureAwait(false)
                    ? Results.NoContent()
                    : AdminResults.Error(StatusCodes.Status400BadRequest, refusal)
        );

    private static IResult Saved(int id) => Results.Ok(new { id });

    private static IResult Removed(bool removed) =>
        removed ? Results.NoContent() : Results.NotFound();

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
