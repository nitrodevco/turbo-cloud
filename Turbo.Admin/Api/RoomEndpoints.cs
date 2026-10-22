using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Admin.Configuration;
using Turbo.Admin.Rooms;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;

namespace Turbo.Admin.Api;

/// <summary>
/// Finding, inspecting, editing and moderating rooms. Looking needs <c>admin.rooms.view</c> and
/// never loads a room. Every change asks for the node the hotel asks for
/// (<see cref="AdminRoomAbilities"/>) and goes through the room grain or room service acting as the
/// staff member, so the room applies its own rules: who outranks whom, group rooms' rights, the
/// settings' limits. Actions on the people inside need the room loaded; the rest (settings,
/// rights, bans, staff pick) load it lightly, without its furniture, and the bots and pets
/// standing in it are set through the room grain. Each is logged with who made it.
/// </summary>
internal sealed class RoomEndpoints(
    IGrainFactory grainFactory,
    AdminRoomQueries rooms,
    AdminRoomVisits visits,
    AdminRoomBots bots,
    AdminRoomPets pets,
    AdminRoomEditor editor,
    IRoomService roomService,
    IRoomOwnershipService ownership,
    AdminActionLog actions,
    IOptions<AdminConfig> config,
    ILogger<RoomEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't look up rooms.";

    private static readonly IResult NOT_LOADED = AdminResults.Error(
        StatusCodes.Status409Conflict,
        "The room is not loaded, so nobody is in it."
    );

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/rooms")
            .RequirePermission(PermissionNodes.Admin.ROOMS_VIEW, NO_ACCESS)
            .ChecksInHandler(
                "A change asks AdminRoomAbilities for the node the hotel asks for in that room, where owning it counts."
            );

        group.MapGet("/", SearchAsync);
        group.MapGet("/categories", CategoriesAsync);
        group.MapGet("/{id:int}", GetAsync);
        group.MapGet("/{id:int}/visitors", VisitorsAsync);
        group.MapGet("/{id:int}/bots", BotsAsync);
        group.MapPut("/{id:int}/bots/{botId:int}", SaveBotAsync);
        group.MapPost("/{id:int}/bots/{botId:int}/pickup", PickupBotAsync);
        group.MapGet("/{id:int}/pets", PetsAsync);
        group.MapPut("/{id:int}/pets/{petId:int}", SavePetAsync);
        group.MapPost("/{id:int}/pets/{petId:int}/pickup", PickupPetAsync);
        group.MapPut("/{id:int}/settings", SaveSettingsAsync);
        group.MapPut("/{id:int}/staff-pick", SetStaffPickAsync);
        group.MapPut("/{id:int}/owner", TransferOwnerAsync);
        group.MapPost("/{id:int}/kick", KickAsync);
        group.MapPost("/{id:int}/mute", MuteAsync);
        group.MapPost("/{id:int}/ban", BanAsync);
        group.MapDelete("/{id:int}/bans/{playerId:int}", UnbanAsync);
        group.MapDelete("/{id:int}/rights/{playerId:int}", RemoveRightsAsync);
        group.MapDelete("/{id:int}/rights", RemoveAllRightsAsync);
        group.MapPost("/{id:int}/kick-all", KickAllAsync);
        group.MapPut("/{id:int}/muted", SetMutedAsync);
        group.MapPost("/{id:int}/unload", UnloadAsync);
        group.MapPost("/{id:int}/alert", AlertAsync);
    }

    private async Task<IResult> SearchAsync(
        string? q,
        string? by,
        int? page,
        CancellationToken ct
    ) => Results.Ok(await rooms.SearchAsync(q, ModeOf(by), page ?? 1, ct).ConfigureAwait(false));

    private async Task<IResult> CategoriesAsync(CancellationToken ct) =>
        Results.Ok(await rooms.GetCategoriesAsync(ct).ConfigureAwait(false));

    private async Task<IResult> GetAsync(HttpContext http, int id, CancellationToken ct) =>
        await rooms.GetAsync(id, AdminIdentity.Of(http).PlayerId, ct).ConfigureAwait(false)
            is { } room
            ? Results.Ok(room)
            : NoSuchRoom(id);

    private async Task<IResult> VisitorsAsync(int id, CancellationToken ct) =>
        Results.Ok(await visits.ForRoomAsync(id, ct).ConfigureAwait(false));

    private async Task<IResult> BotsAsync(int id, CancellationToken ct) =>
        Results.Ok(await bots.ListAsync(id, ct).ConfigureAwait(false));

    private async Task<IResult> SaveBotAsync(
        HttpContext http,
        int id,
        int botId,
        RoomBotRequest request,
        CancellationToken ct
    )
    {
        if (
            await RefuseAsync(http, id, x => x.ManageBotsAndPets, ct).ConfigureAwait(false) is
            { } no
        )
            return no;

        if (!await bots.UpdateAsync(id, botId, request, ct).ConfigureAwait(false))
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Not saved: the name is too short or too long, or the bot has left the room."
            );

        Log(http, "set bot {BotId} in room {RoomId}", botId, id);

        return Done("Saved.");
    }

    private async Task<IResult> PickupBotAsync(
        HttpContext http,
        int id,
        int botId,
        CancellationToken ct
    )
    {
        if (
            await RefuseAsync(http, id, x => x.ManageBotsAndPets, ct).ConfigureAwait(false) is
            { } no
        )
            return no;

        if (!await bots.PickupAsync(id, botId, ct).ConfigureAwait(false))
            return Refused("The bot doesn't stand in this room.");

        Log(http, "took bot {BotId} out of room {RoomId}", botId, id);

        return Done("Taken back to its owner's inventory.");
    }

    private async Task<IResult> PetsAsync(int id, CancellationToken ct) =>
        Results.Ok(await pets.ListAsync(id, ct).ConfigureAwait(false));

    private async Task<IResult> SavePetAsync(
        HttpContext http,
        int id,
        int petId,
        RoomPetRequest request,
        CancellationToken ct
    )
    {
        if (
            await RefuseAsync(http, id, x => x.ManageBotsAndPets, ct).ConfigureAwait(false) is
            { } no
        )
            return no;

        if (!await pets.UpdateAsync(id, petId, request, ct).ConfigureAwait(false))
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Not saved: the name is too short or too long, or the pet has left the room."
            );

        Log(http, "set pet {PetId} in room {RoomId}", petId, id);

        return Done("Saved.");
    }

    private async Task<IResult> PickupPetAsync(
        HttpContext http,
        int id,
        int petId,
        CancellationToken ct
    )
    {
        if (
            await RefuseAsync(http, id, x => x.ManageBotsAndPets, ct).ConfigureAwait(false) is
            { } no
        )
            return no;

        if (!await pets.PickupAsync(id, petId, ct).ConfigureAwait(false))
            return Refused("The pet doesn't stand in this room.");

        Log(http, "took pet {PetId} out of room {RoomId}", petId, id);

        return Done("Taken back to its owner's inventory.");
    }

    private async Task<IResult> SaveSettingsAsync(
        HttpContext http,
        int id,
        RoomSettingsRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseAsync(http, id, x => x.EditSettings, ct).ConfigureAwait(false) is { } no)
            return no;

        var result = await editor
            .SaveSettingsAsync(AdminIdentity.Of(http).PlayerId, id, request, ct)
            .ConfigureAwait(false);

        if (result.Outcome == AdminRoomSaveOutcome.Saved)
            Log(http, "saved the settings of room {RoomId}", id);

        return result.Outcome switch
        {
            AdminRoomSaveOutcome.Saved => Done(result.Message),
            AdminRoomSaveOutcome.NotFound => NoSuchRoom(id),
            _ => AdminResults.Error(StatusCodes.Status400BadRequest, result.Message),
        };
    }

    private async Task<IResult> SetStaffPickAsync(
        HttpContext http,
        int id,
        RoomStaffPickRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseAsync(http, id, x => x.StaffPick, ct).ConfigureAwait(false) is { } no)
            return no;

        await Room(id).SetStaffPickAsync(request.StaffPick, ct).ConfigureAwait(false);
        Log(http, "set room {RoomId} staff pick to {StaffPick}", id, request.StaffPick);

        return Done(
            request.StaffPick ? "The room is a staff pick." : "The room is no longer a staff pick."
        );
    }

    /// <summary>
    /// Gives the room to the named player through the room ownership service, loaded or not, and
    /// writes the request to the command log whatever came of it.
    /// </summary>
    private async Task<IResult> TransferOwnerAsync(
        HttpContext http,
        int id,
        RoomOwnerRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseAsync(http, id, x => x.TransferOwner, ct).ConfigureAwait(false) is { } no)
            return no;

        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Name the player to give the room to."
            );

        if (
            await grainFactory
                .GetPlayerDirectoryGrain()
                .GetPlayerIdAsync(name, ct)
                .ConfigureAwait(false)
            is not { } playerId
        )
            return AdminResults.Error(
                StatusCodes.Status404NotFound,
                $"There is no player called {name}."
            );

        var outcome = await ownership
            .TransferAsync(new RoomId(id), playerId, ct)
            .ConfigureAwait(false);
        var transferred = outcome == RoomOwnerTransferOutcome.Transferred;

        await actions
            .WriteAsync(
                AdminActionLog.ROOM,
                AdminIdentity.Of(http).PlayerId,
                $"owner {id} {playerId.Value}",
                transferred ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        if (transferred)
            Log(http, "gave room {RoomId} to player {PlayerId}", id, playerId.Value);

        return outcome switch
        {
            RoomOwnerTransferOutcome.Transferred => Done($"The room is {name}'s now."),
            RoomOwnerTransferOutcome.NoSuchRoom => NoSuchRoom(id),
            RoomOwnerTransferOutcome.NoSuchPlayer => AdminResults.Error(
                StatusCodes.Status404NotFound,
                $"There is no player called {name}."
            ),
            RoomOwnerTransferOutcome.AlreadyOwner => Refused($"{name} owns the room already."),
            RoomOwnerTransferOutcome.GroupHomeRoom => Refused(
                "The room is a group's home room, and this hotel keeps those with their owner."
            ),
            RoomOwnerTransferOutcome.Deleting => Refused("The room is being deleted."),
            _ => AdminResults.Error(
                StatusCodes.Status500InternalServerError,
                "The room could not be given; nothing changed."
            ),
        };
    }

    private async Task<IResult> KickAsync(
        HttpContext http,
        int id,
        RoomPlayerRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseLoadedAsync(http, id, x => x.Moderate, ct).ConfigureAwait(false) is { } no)
            return no;

        if (
            !await roomService
                .KickPlayerAsync(Acting(http, id), request.PlayerId, ct)
                .ConfigureAwait(false)
        )
            return Refused(
                "The room refused: they are not inside, or they outrank you in this room."
            );

        Log(http, "kicked player {PlayerId} from room {RoomId}", request.PlayerId, id);

        return Done("Kicked.");
    }

    private async Task<IResult> MuteAsync(
        HttpContext http,
        int id,
        RoomMuteRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseLoadedAsync(http, id, x => x.Moderate, ct).ConfigureAwait(false) is { } no)
            return no;

        var minutes = Math.Clamp(request.Minutes, 1, config.Value.RoomMuteMaxMinutes);

        if (
            !await Room(id)
                .MutePlayerAsync(Acting(http, id), request.PlayerId, minutes, ct)
                .ConfigureAwait(false)
        )
            return Refused(
                "The room refused: they are not inside, or they outrank you in this room."
            );

        Log(
            http,
            "muted player {PlayerId} in room {RoomId} for {Minutes} min",
            request.PlayerId,
            id,
            minutes
        );

        return Done($"Muted for {minutes} min.");
    }

    private async Task<IResult> BanAsync(
        HttpContext http,
        int id,
        RoomBanRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseAsync(http, id, x => x.Moderate, ct).ConfigureAwait(false) is { } no)
            return no;

        if (
            !Enum.TryParse<RoomBanDurationType>(request.Duration, true, out var duration)
            || !Enum.IsDefined(duration)
        )
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "A ban lasts an hour, a day, or for good."
            );

        var target = request.PlayerId is > 0 and var playerId
            ? new PlayerId(playerId)
            : await grainFactory
                .GetPlayerDirectoryGrain()
                .GetPlayerIdAsync(request.Name?.Trim() ?? string.Empty, ct)
                .ConfigureAwait(false);

        if (target is not { } banned)
            return AdminResults.Error(
                StatusCodes.Status404NotFound,
                $"No player is called {request.Name}."
            );

        if (
            !await roomService
                .BanPlayerAsync(Acting(http, id), banned, duration, ct)
                .ConfigureAwait(false)
        )
            return Refused("The room refused: they outrank you in this room.");

        Log(
            http,
            "banned player {PlayerId} from room {RoomId} ({Duration})",
            banned.Value,
            id,
            duration
        );

        return Done("Banned.");
    }

    private async Task<IResult> UnbanAsync(
        HttpContext http,
        int id,
        int playerId,
        CancellationToken ct
    )
    {
        if (await RefuseAsync(http, id, x => x.Moderate, ct).ConfigureAwait(false) is { } no)
            return no;

        if (!await Room(id).UnbanPlayerAsync(Acting(http, id), playerId, ct).ConfigureAwait(false))
            return Refused("The room refused to lift that ban.");

        Log(http, "lifted the ban of player {PlayerId} in room {RoomId}", playerId, id);

        return Done("Ban lifted.");
    }

    private async Task<IResult> RemoveRightsAsync(
        HttpContext http,
        int id,
        int playerId,
        CancellationToken ct
    )
    {
        if (await RefuseAsync(http, id, x => x.ManageRights, ct).ConfigureAwait(false) is { } no)
            return no;

        await Room(id)
            .RemoveRightsFromPlayerAsync(Acting(http, id), playerId, ct)
            .ConfigureAwait(false);

        // The room says nothing when it refuses (a group room's rights come from the group).
        if (await rooms.HasRightsAsync(id, playerId, ct).ConfigureAwait(false))
            return Refused(
                "The room kept their rights. A group room's rights come from its group."
            );

        Log(http, "took rights from player {PlayerId} in room {RoomId}", playerId, id);

        return Done("Rights removed.");
    }

    private async Task<IResult> RemoveAllRightsAsync(HttpContext http, int id, CancellationToken ct)
    {
        if (await RefuseAsync(http, id, x => x.ManageRights, ct).ConfigureAwait(false) is { } no)
            return no;

        await Room(id).RemoveAllRightsAsync(Acting(http, id), ct).ConfigureAwait(false);
        Log(http, "took everyone's rights in room {RoomId}", id);

        return Done("Everyone's rights were removed.");
    }

    private async Task<IResult> KickAllAsync(HttpContext http, int id, CancellationToken ct)
    {
        if (await RefuseLoadedAsync(http, id, x => x.KickAll, ct).ConfigureAwait(false) is { } no)
            return no;

        var cleared = await Room(id)
            .ClearRoomBySystemAsync(AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false);
        Log(http, "sent {Count} players out of room {RoomId}", cleared, id);

        return Done(
            cleared == 1 ? "1 visitor was sent out." : $"{cleared} visitors were sent out."
        );
    }

    private async Task<IResult> SetMutedAsync(
        HttpContext http,
        int id,
        RoomMutedRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseLoadedAsync(http, id, x => x.MuteRoom, ct).ConfigureAwait(false) is { } no)
            return no;

        // Either node shows the switch (RoomAbilities.MuteRoom), but each way needs its own, as
        // :roommute and :roomunmute do in the hotel.
        if (
            !await AdminIdentity
                .Of(http)
                .HoldsAsync(
                    grainFactory,
                    request.Muted
                        ? PermissionNodes.Command.ROOMMUTE
                        : PermissionNodes.Command.ROOMUNMUTE,
                    ct
                )
                .ConfigureAwait(false)
        )
            return AdminResults.Error(
                StatusCodes.Status403Forbidden,
                request.Muted ? "You can't mute rooms." : "You can't lift a room's mute."
            );

        var changed = await Room(id)
            .SetRoomMutedBySystemAsync(request.Muted, ct)
            .ConfigureAwait(false);

        if (changed)
            Log(http, "set room {RoomId} muted to {Muted}", id, request.Muted);

        return Done(
            request.Muted
                ? changed
                    ? "The room is muted."
                    : "The room was already muted."
                : changed
                    ? "The room is no longer muted."
                    : "The room was not muted."
        );
    }

    private async Task<IResult> UnloadAsync(HttpContext http, int id, CancellationToken ct)
    {
        if (await RefuseLoadedAsync(http, id, x => x.Unload, ct).ConfigureAwait(false) is { } no)
            return no;

        await Room(id).EvictEveryoneAndUnloadAsync(ct).ConfigureAwait(false);
        Log(http, "unloaded room {RoomId}", id);

        return Done("The room was unloaded.");
    }

    private async Task<IResult> AlertAsync(
        HttpContext http,
        int id,
        RoomAlertRequest request,
        CancellationToken ct
    )
    {
        if (await RefuseLoadedAsync(http, id, x => x.Alert, ct).ConfigureAwait(false) is { } no)
            return no;

        var message = request.Message?.Trim() ?? string.Empty;

        if (message.Length == 0 || message.Length > config.Value.RoomAlertMaxLength)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"An alert is 1 to {config.Value.RoomAlertMaxLength} characters."
            );

        await Room(id)
            .SendComposerToRoomAsync(new HabboBroadcastMessageComposer { Message = message }, ct)
            .ConfigureAwait(false);
        Log(http, "sent an alert to room {RoomId}", id);

        return Done("Alert sent.");
    }

    /// <summary>
    /// Null when the room exists and the viewer may do this to it; otherwise the answer to send.
    /// </summary>
    private async Task<IResult?> RefuseAsync(
        HttpContext http,
        int id,
        Func<RoomAbilities, bool> may,
        CancellationToken ct
    )
    {
        if (await rooms.GetOwnerIdAsync(id, ct).ConfigureAwait(false) is not { } ownerId)
            return NoSuchRoom(id);

        var can = await AdminRoomAbilities
            .ForAsync(grainFactory, AdminIdentity.Of(http).PlayerId, ownerId, ct)
            .ConfigureAwait(false);

        return may(can)
            ? null
            : AdminResults.Error(
                StatusCodes.Status403Forbidden,
                "You don't have the permission this needs in this room."
            );
    }

    /// <summary>As <see cref="RefuseAsync"/>, for actions on the people inside, which never load the room.</summary>
    private async Task<IResult?> RefuseLoadedAsync(
        HttpContext http,
        int id,
        Func<RoomAbilities, bool> may,
        CancellationToken ct
    ) =>
        await RefuseAsync(http, id, may, ct).ConfigureAwait(false)
        ?? (await rooms.IsLoadedAsync(id, ct).ConfigureAwait(false) ? null : NOT_LOADED);

    private void Log(HttpContext http, string action, params object[] args) =>
#pragma warning disable CA2254 // The template is one of this class's own constants, with its holes.
        logger.LogInformation(
            "Admin panel: {Admin} " + action,
            [AdminIdentity.Of(http).Name, .. args]
        );
#pragma warning restore CA2254

    /// <summary>The staff member, acting on the room as if standing in it.</summary>
    private static ActionContext Acting(HttpContext http, int id) =>
        ActionContext.CreateForPlayer(AdminIdentity.Of(http).PlayerId, new RoomId(id));

    private IRoomGrain Room(int id) => grainFactory.GetRoomGrain(new RoomId(id));

    private static IResult Done(string message) => Results.Ok(new RoomActionResponse(message));

    private static IResult Refused(string message) =>
        AdminResults.Error(StatusCodes.Status409Conflict, message);

    private static IResult NoSuchRoom(int id) =>
        AdminResults.Error(StatusCodes.Status404NotFound, $"There is no room {id}.");

    private static RoomSearchMode ModeOf(string? by) =>
        Enum.TryParse<RoomSearchMode>(by, ignoreCase: true, out var mode)
            ? mode
            : RoomSearchMode.Name;
}
