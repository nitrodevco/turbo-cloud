using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Room;
using Turbo.Furniture.Custody;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Navigator.Onboarding;

/// <summary>
/// <see cref="IStarterRoomService"/>. A room type is a room of the hotel's starter rooms
/// (<see cref="IStarterRoomDirectory"/>, the type being its id), copied for the player; with the
/// hotel offering none, the built-in templates: embedded <c>StarterRooms/&lt;type&gt;.json</c>
/// files recorded from Habbo (the model, the paint, the name and description and the furni with
/// their places).
///
/// The room is made through the navigator like any other (so the room limit and model checks
/// hold), then painted and furnished in one write before anyone enters it. Furni the hotel has no
/// definition for are left out and logged. A copied room takes its source's model - its own
/// floor plan copied too - its paint and walls and every furni in it with its state; a
/// teleporter's link to its partner is not copied, and a wired box keeps the source's settings.
/// </summary>
public sealed class StarterRoomService(
    INavigatorService navigatorService,
    IFurnitureDefinitionProvider furnitureDefinitions,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IFurnitureCustody custody,
    IGrainFactory grainFactory,
    IOptions<NavigatorConfig> navigatorConfig,
    IStarterRoomDirectory directory,
    ILogger<IStarterRoomService> logger
) : IStarterRoomService
{
    private const string RESOURCE_PREFIX = "StarterRooms.";

    /// <summary>The player's name in a starter room's name and description.</summary>
    private const string NAME_PLACEHOLDER = "%name%";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<FrozenDictionary<string, StarterRoom>> TEMPLATES = new(
        LoadTemplates
    );

    public async Task<bool> HasTypeAsync(string roomType, CancellationToken ct) =>
        await ResolveAsync(roomType, ct).ConfigureAwait(false) is not null;

    public async Task<CreatedStarterRoom?> CreateAsync(
        PlayerId playerId,
        string roomType,
        CancellationToken ct
    )
    {
        var option = await ResolveAsync(roomType, ct).ConfigureAwait(false);

        if (option is null)
            return null;

        var ownerName = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct)
            .ConfigureAwait(false);

        var created =
            (option.RoomId > 0)
                ? await CopyRoomAsync(playerId, ownerName, option, ct).ConfigureAwait(false)
                : await FromTemplateAsync(playerId, ownerName, option, ct).ConfigureAwait(false);

        if (created is not null)
            logger.LogInformation(
                "Made starter room {RoomId} (type {RoomType}, from {Source}) for player {PlayerId}",
                created.RoomId,
                option.Type,
                (option.RoomId > 0) ? $"room {option.RoomId}" : "its template",
                playerId
            );

        return created;
    }

    /// <summary>The hotel's starter room of that id, or - with the hotel offering none - the built-in template's.</summary>
    private async Task<StarterRoomOption?> ResolveAsync(string roomType, CancellationToken ct)
    {
        var type = roomType.Trim();
        var rooms = await directory.ListAsync(ct).ConfigureAwait(false);

        if (!rooms.IsEmpty)
        {
            var room = rooms.FirstOrDefault(x =>
                string.Equals(
                    x.RoomId.Value.ToString(CultureInfo.InvariantCulture),
                    type,
                    StringComparison.Ordinal
                )
            );

            return room is null
                ? null
                : new StarterRoomOption(type, room.RoomId.Value, room.Name, room.Description);
        }

        return TEMPLATES.Value.ContainsKey(type)
            ? new StarterRoomOption(type, 0, null, null)
            : null;
    }

    /// <summary>What a pick makes: a copy of a room (<c>RoomId</c>), or a template's room.</summary>
    private sealed record StarterRoomOption(
        string Type,
        int RoomId,
        string? Name,
        string? Description
    );

    private static string Fill(string pattern, string ownerName) =>
        pattern.Replace(NAME_PLACEHOLDER, ownerName, StringComparison.OrdinalIgnoreCase);

    private async Task<CreatedStarterRoom?> FromTemplateAsync(
        PlayerId playerId,
        string ownerName,
        StarterRoomOption option,
        CancellationToken ct
    )
    {
        var template = TEMPLATES.Value[option.Type.Trim()];
        var name = Fill(
            string.IsNullOrWhiteSpace(option.Name) ? template.Name : option.Name,
            ownerName
        );
        var description = Fill(
            string.IsNullOrWhiteSpace(option.Description)
                ? template.Description
                : option.Description,
            ownerName
        );

        var roomId = await navigatorService
            .CreateRoomAsync(
                playerId,
                name,
                description,
                template.Model,
                template.Category,
                Math.Min(template.MaxUsers, navigatorConfig.Value.MaxPlayersLimit),
                RoomTradeModeType.Disabled,
                ct
            )
            .ConfigureAwait(false);

        if (roomId is null)
            return null;

        await FurnishAsync(roomId.Value, playerId, template, ct).ConfigureAwait(false);

        return new(roomId.Value, name);
    }

    /// <summary>A copy of a hotel room: made through the navigator, then given the source's model, look and furni.</summary>
    private async Task<CreatedStarterRoom?> CopyRoomAsync(
        PlayerId playerId,
        string ownerName,
        StarterRoomOption option,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var source = await db
            .Rooms.AsNoTracking()
            .Include(x => x.RoomModelEntity)
            .SingleOrDefaultAsync(x => x.Id == option.RoomId, ct)
            .ConfigureAwait(false);

        if (source is null)
        {
            logger.LogWarning(
                "Starter room type {RoomType} copies room {RoomId}, which does not exist",
                option.Type,
                option.RoomId
            );

            return null;
        }

        // A room with a floor plan of its own is made on any public model first and given a copy
        // of that plan below; the navigator only makes rooms on the public ones.
        var modelName = source.RoomModelEntity.Custom
            ? await db
                .RoomModels.AsNoTracking()
                .Where(x => x.Enabled && !x.Custom)
                .OrderBy(x => x.Id)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false)
            : source.RoomModelEntity.Name;

        var name = Fill(
            string.IsNullOrWhiteSpace(option.Name) ? source.Name : option.Name,
            ownerName
        );
        var description = Fill(
            string.IsNullOrWhiteSpace(option.Description)
                ? source.Description ?? string.Empty
                : option.Description,
            ownerName
        );

        var roomId = await navigatorService
            .CreateRoomAsync(
                playerId,
                name,
                description,
                modelName ?? string.Empty,
                source.NavigatorCategoryEntityId ?? 0,
                Math.Min(source.PlayersMax, navigatorConfig.Value.MaxPlayersLimit),
                source.TradeType,
                ct
            )
            .ConfigureAwait(false);

        if (roomId is null)
            return null;

        var newRoomId = roomId.Value.Value;
        var room = await db.Rooms.SingleAsync(x => x.Id == newRoomId, ct).ConfigureAwait(false);

        if (source.RoomModelEntity.Custom)
        {
            var model = new RoomModelEntity
            {
                Name = "room_" + newRoomId,
                Model = source.RoomModelEntity.Model,
                DoorX = source.RoomModelEntity.DoorX,
                DoorY = source.RoomModelEntity.DoorY,
                DoorRotation = source.RoomModelEntity.DoorRotation,
                Enabled = true,
                Custom = true,
            };

            db.RoomModels.Add(model);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            room.RoomModelEntityId = model.Id;
        }

        room.PaintWall = source.PaintWall;
        room.PaintFloor = source.PaintFloor;
        room.PaintLandscape = source.PaintLandscape;
        room.WallHeight = source.WallHeight;
        room.HideWalls = source.HideWalls;
        room.ThicknessWall = source.ThicknessWall;
        room.ThicknessFloor = source.ThicknessFloor;

        var items = await db
            .Furnitures.AsNoTracking()
            .Where(x => x.RoomEntityId == source.Id)
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // The look and the furni in one write, the furni through the custody.
        var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        await custody
            .CreateInRoomAsync(
                db,
                roomId.Value,
                playerId,
                [
                    .. items.Select(item => new FurniToPlace(
                        new FurniToMake(
                            item.FurnitureDefinitionEntityId,
                            WithoutTeleportLink(item.ExtraData)
                        ),
                        new RoomPosition(item.X, item.Y, item.Z, item.Rotation, item.WallOffset)
                    )),
                ],
                StarterRoomCause(playerId, roomId.Value),
                ct
            )
            .ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        return new(roomId.Value, name);
    }

    /// <summary>The item's data without its teleporter partner, which is the source room's.</summary>
    private static string? WithoutTeleportLink(string? extraData)
    {
        if (string.IsNullOrWhiteSpace(extraData))
            return extraData;

        try
        {
            if (JsonNode.Parse(extraData) is JsonObject data && data.Remove(RoomLinkerData.SECTION))
                return data.ToJsonString();
        }
        catch (JsonException)
        {
            // Not JSON: kept as it is.
        }

        return extraData;
    }

    /// <summary>Paints the new room and puts its furni in place, all in one write.</summary>
    private async Task FurnishAsync(
        RoomId roomId,
        PlayerId ownerId,
        StarterRoom template,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var room = await db.Rooms.SingleAsync(x => x.Id == roomId.Value, ct).ConfigureAwait(false);

        room.PaintWall = template.Wallpaper;
        room.PaintFloor = template.Floor;
        room.PaintLandscape = template.Landscape;

        List<string> missing = [];
        List<FurniToPlace> furni = [];

        foreach (var item in template.FloorItems)
        {
            if (Definition(item.Name, missing) is not { } definition)
                continue;

            furni.Add(
                new FurniToPlace(
                    new FurniToMake(definition, State(item.State)),
                    new RoomPosition(item.X, item.Y, item.Z, (Rotation)item.Rotation, null)
                )
            );
        }

        foreach (var item in template.WallItems)
        {
            if (!WallPosition.TryParse(item.Position, out var position))
            {
                logger.LogWarning(
                    "Starter room {RoomType}: wall position {Position} of {Name} does not read",
                    template.Type,
                    item.Position,
                    item.Name
                );

                continue;
            }

            if (Definition(item.Name, missing) is not { } definition)
                continue;

            furni.Add(
                new FurniToPlace(
                    new FurniToMake(definition, State(item.State)),
                    new RoomPosition(
                        position.X,
                        position.Y,
                        position.Z,
                        position.Rotation,
                        position.WallOffset
                    )
                )
            );
        }

        var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using var txScope = tx.ConfigureAwait(false);

        await custody
            .CreateInRoomAsync(db, roomId, ownerId, furni, StarterRoomCause(ownerId, roomId), ct)
            .ConfigureAwait(false);

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        if (missing.Count > 0)
            logger.LogWarning(
                "Starter room {RoomType} made without {Missing}: the hotel has no such furni",
                template.Type,
                string.Join(", ", missing.Distinct())
            );
    }

    /// <summary>A new player's room furnished for them: their own action, the room what caused it.</summary>
    private static MovementCause StarterRoomCause(PlayerId playerId, RoomId roomId) =>
        MovementCause.New(MovementCauses.ONBOARDING_STARTER_ROOM, playerId.Value, roomId.Value);

    private int? Definition(string name, List<string> missing)
    {
        var definition = furnitureDefinitions.TryGetDefinitionByName(name);

        if (definition is null)
            missing.Add(name);

        return definition?.Id;
    }

    /// <summary>The recorded state as legacy stuff data; none for no state ("" or -1).</summary>
    private static string? State(string? state) =>
        string.IsNullOrWhiteSpace(state) || state.Trim() == "-1"
            ? null
            : ProductStuffData.ExtraData(state);

    private static FrozenDictionary<string, StarterRoom> LoadTemplates()
    {
        var assembly = typeof(StarterRoomService).Assembly;
        var templates = new Dictionary<string, StarterRoom>(StringComparer.Ordinal);

        foreach (
            var resource in assembly
                .GetManifestResourceNames()
                .Where(x => x.StartsWith(RESOURCE_PREFIX, StringComparison.Ordinal))
        )
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            var template =
                JsonSerializer.Deserialize<StarterRoom>(stream, JSON_OPTIONS)
                ?? throw new InvalidDataException($"Starter room {resource} is empty.");

            templates[template.Type] = template;
        }

        return templates.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private sealed record StarterRoom(
        string Type,
        string Model,
        string Name,
        string Description,
        int Category,
        int MaxUsers,
        string? Wallpaper,
        string? Floor,
        string? Landscape,
        List<StarterFloorItem> FloorItems,
        List<StarterWallItem> WallItems
    );

    private sealed record StarterFloorItem(
        string Name,
        int X,
        int Y,
        double Z,
        int Rotation,
        string? State
    );

    private sealed record StarterWallItem(string Name, string Position, string? State);
}
