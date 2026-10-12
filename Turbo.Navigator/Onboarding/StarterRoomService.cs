using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Navigator.Configuration;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Navigator.Onboarding;

/// <summary>
/// <see cref="IStarterRoomService"/>. Each room type is an embedded <c>StarterRooms/&lt;type&gt;.json</c>
/// recorded from Habbo: the model, the paint, the room's name and description patterns and the
/// furni with their places. The room is made through the navigator like any other (so the room
/// limit and model checks hold), then painted and furnished in one write before anyone enters it.
/// Furni the hotel has no definition for are left out and logged.
/// </summary>
public sealed class StarterRoomService(
    INavigatorService navigatorService,
    IFurnitureDefinitionProvider furnitureDefinitions,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    IOptions<NavigatorConfig> navigatorConfig,
    ILogger<IStarterRoomService> logger
) : IStarterRoomService
{
    private const string RESOURCE_PREFIX = "StarterRooms.";

    private static readonly JsonSerializerOptions JSON_OPTIONS = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Lazy<FrozenDictionary<string, StarterRoom>> TEMPLATES = new(
        LoadTemplates
    );

    public bool HasType(string roomType) => TEMPLATES.Value.ContainsKey(roomType.Trim());

    public async Task<CreatedStarterRoom?> CreateAsync(
        PlayerId playerId,
        string roomType,
        CancellationToken ct
    )
    {
        if (!TEMPLATES.Value.TryGetValue(roomType.Trim(), out var template))
            return null;

        var ownerName = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct)
            .ConfigureAwait(false);

        var name = string.Format(template.Name, ownerName);
        var roomId = await navigatorService
            .CreateRoomAsync(
                playerId,
                name,
                string.Format(template.Description, ownerName),
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

        logger.LogInformation(
            "Made starter room {RoomId} (type {RoomType}) for player {PlayerId}",
            roomId,
            template.Type,
            playerId
        );

        return new(roomId.Value, name);
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

        foreach (var item in template.FloorItems)
        {
            if (Definition(item.Name, missing) is not { } definition)
                continue;

            db.Add(
                new FurnitureEntity
                {
                    PlayerEntityId = ownerId.Value,
                    FurnitureDefinitionEntityId = definition,
                    RoomEntityId = roomId.Value,
                    X = item.X,
                    Y = item.Y,
                    Z = item.Z,
                    Rotation = (Rotation)item.Rotation,
                    ExtraData = State(item.State),
                }
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

            db.Add(
                new FurnitureEntity
                {
                    PlayerEntityId = ownerId.Value,
                    FurnitureDefinitionEntityId = definition,
                    RoomEntityId = roomId.Value,
                    X = position.X,
                    Y = position.Y,
                    Z = position.Z,
                    Rotation = position.Rotation,
                    WallOffset = position.WallOffset,
                    ExtraData = State(item.State),
                }
            );
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (missing.Count > 0)
            logger.LogWarning(
                "Starter room {RoomType} made without {Missing}: the hotel has no such furni",
                template.Type,
                string.Join(", ", missing.Distinct())
            );
    }

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
