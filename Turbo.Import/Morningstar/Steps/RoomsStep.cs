using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Import.Morningstar.Formats;
using Turbo.Import.Source;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// <c>rooms</c>, with their own floor plans (<c>room_models_custom</c>, used while
/// <c>override_model</c> is set), rights, bans, votes, word filters and the players who made
/// them a favourite. Ids are kept. A room whose owner was not copied, or whose model neither
/// hotel has, is left out, and so is everything in it.
/// </summary>
internal sealed class RoomsStep : IImportStep
{
    private const string FALLBACK_MODEL = "model_a";

    public string Name => "rooms";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var custom = await CustomModelsAsync(ctx, ct).ConfigureAwait(false);
        var rooms = ctx.Batch<RoomEntity>("rooms");

        await foreach (var row in ctx.Source.ReadAsync("rooms", "id", ct).ConfigureAwait(false))
        {
            var id = row.Int("id");
            var owner = row.Int("owner_id");

            if (!ctx.Players.Contains(owner))
            {
                ctx.Report.Note("room left out: its owner was not copied", $"room {id}");

                continue;
            }

            var model = await ModelAsync(ctx, row, id, custom, ct).ConfigureAwait(false);

            if (model is null)
                continue;

            ctx.RoomOwners[id] = owner;

            if (row.String("moodlight_data") is { Length: > 0 } moodlight)
                ctx.RoomMoodlights[id] = moodlight;

            await rooms.AddAsync(Room(ctx, row, id, owner, model.Value), ct).ConfigureAwait(false);
        }

        await rooms.FlushAsync(ct).ConfigureAwait(false);

        await RightsAsync(ctx, ct).ConfigureAwait(false);
        await BansAsync(ctx, ct).ConfigureAwait(false);
        await VotesAsync(ctx, ct).ConfigureAwait(false);
        await WordFiltersAsync(ctx, ct).ConfigureAwait(false);
        await FavouritesAsync(ctx, ct).ConfigureAwait(false);
    }

    private static RoomEntity Room(ImportContext ctx, SourceRow row, int id, int owner, int model)
    {
        var category = row.Int("category");
        var limits = ctx.Options.Limits;

        return new RoomEntity
        {
            Id = id,
            CreatedAt = ctx.Now,
            LastActive = ctx.Now,
            Name = ClientText.Truncate(row.Text("name", $"Room {id}"), limits.RoomNameMaxLength),
            Description = row.String("description") is { } description
                ? ClientText.Truncate(description, limits.RoomDescriptionMaxLength)
                : null,
            PlayerEntityId = owner,
            DoorMode = DoorMode(row.Text("state")),
            Password = row.String("password") is { Length: > 0 } password ? password : null,
            RoomModelEntityId = model,
            NavigatorCategoryEntityId = ctx.FlatCategories.TryGetValue(category, out var c)
                ? c
                : null,
            UsersNow = 0,
            PlayersMax = Math.Clamp(
                row.Int("users_max", 25),
                1,
                Math.Max(1, limits.RoomPlayersMax)
            ),
            PaintWall = Paint(row.String("paper_wall")),
            PaintFloor = Paint(row.String("paper_floor")),
            PaintLandscape = Paint(row.String("paper_landscape")),
            WallHeight = row.Int("wall_height", -1),
            HideWalls = row.Bool("allow_hidewall"),
            ThicknessWall = Thickness(row.Int("thickness_wall")),
            ThicknessFloor = Thickness(row.Int("thickness_floor")),
            // Turbo's allow_blocking is the room setting "allow walk through".
            AllowBlocking = row.Bool("allow_walkthrough", true),
            AllowPets = row.Bool("allow_other_pets"),
            AllowPetsEat = row.Bool("allow_other_pets_eat"),
            TradeType = row.Int("trade_mode", 2) switch
            {
                0 => RoomTradeModeType.Disabled,
                1 => RoomTradeModeType.RoomOwnerAndRights,
                _ => RoomTradeModeType.Everyone,
            },
            MuteType = ModSetting(row.Int("who_can_mute")),
            KickType = ModSetting(row.Int("who_can_kick")),
            BanType = ModSetting(row.Int("who_can_ban")),
            ChatFloodType = row.Int("chat_protection", 1) switch
            {
                0 => ChatFloodSensitivityType.Extra,
                2 => ChatFloodSensitivityType.Minimal,
                _ => ChatFloodSensitivityType.Normal,
            },
            Score = Math.Max(0, row.Int("score")),
            Tags = Tags(row.String("tags"), limits),
            StaffPick = row.Bool("is_staff_picked"),
            StaffPickedAt = row.Bool("is_staff_picked") ? ctx.Now : null,
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
    }

    /// <summary>
    /// The room's model: its own floor plan while <c>override_model</c> is set and Morningstar
    /// has one, else the named model; a name neither hotel has falls back to
    /// <c>model_a</c>, which is said in the report.
    /// </summary>
    private static async Task<int?> ModelAsync(
        ImportContext ctx,
        SourceRow row,
        int id,
        Dictionary<int, RoomModelEntity> custom,
        CancellationToken ct
    )
    {
        if (row.Bool("override_model") && custom.TryGetValue(id, out var plan))
            return await ctx.AddSetupAsync(plan, RoomModelsStep.TABLE, ct).ConfigureAwait(false);

        var name = row.Text("model").Trim();

        if (ctx.RoomModels.TryGetValue(name, out var model))
            return model;

        if (ctx.RoomModels.TryGetValue(FALLBACK_MODEL, out var fallback))
        {
            ctx.Report.Note(
                $"room's model is unknown; it uses {FALLBACK_MODEL}",
                $"room {id} model '{name}'"
            );

            return fallback;
        }

        ctx.Report.Note("room left out: its model is unknown", $"room {id} model '{name}'");

        return null;
    }

    /// <summary>Each room's own floor plan, read before the rooms and added with its room.</summary>
    private static async Task<Dictionary<int, RoomModelEntity>> CustomModelsAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var plans = new Dictionary<int, RoomModelEntity>();

        await foreach (
            var row in ctx.Source.ReadAsync("room_models_custom", "id", ct).ConfigureAwait(false)
        )
        {
            var room = row.Int("id");
            var name = $"room_{room}";
            var heightmap = Heightmap.Clean(row.String("heightmap"));

            if (heightmap is null)
            {
                ctx.Report.Note(
                    "room's floor plan left out: a tile Turbo cannot read; it uses its model",
                    $"room {room}"
                );

                continue;
            }

            if (ctx.RoomModels.ContainsKey(name))
                continue;

            plans[room] = new RoomModelEntity
            {
                Name = name,
                Model = heightmap,
                DoorX = row.Int("door_x"),
                DoorY = row.Int("door_y"),
                DoorRotation = Directions.Rotation(row.Int("door_dir", 2)),
                Enabled = true,
                Custom = true,
            };
        }

        return plans;
    }

    private static async Task RightsAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<RoomRightEntity>("room_rights");
        var seen = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("room_rights", null, ct).ConfigureAwait(false)
        )
        {
            var room = row.Int("room_id");
            var player = row.Int("user_id");

            // The owner has rights without a row.
            if (
                !ctx.RoomOwners.TryGetValue(room, out var owner)
                || owner == player
                || !ctx.Players.Contains(player)
                || !seen.Add((room, player))
            )
                continue;

            await batch
                .AddAsync(
                    new RoomRightEntity
                    {
                        CreatedAt = ctx.Now,
                        RoomEntityId = room,
                        PlayerEntityId = player,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task BansAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<RoomBanEntity>("room_bans");
        var seen = new HashSet<(int, int)>();

        await foreach (var row in ctx.Source.ReadAsync("room_bans", null, ct).ConfigureAwait(false))
        {
            var room = row.Int("room_id");
            var player = row.Int("user_id");
            var ends = row.UnixTime("ends");

            // A ban that has run out is nothing to carry over.
            if (
                ends is null
                || ends <= ctx.Now
                || !ctx.RoomOwners.ContainsKey(room)
                || !ctx.Players.Contains(player)
                || !seen.Add((room, player))
            )
                continue;

            await batch
                .AddAsync(
                    new RoomBanEntity
                    {
                        CreatedAt = ctx.Now,
                        RoomEntityId = room,
                        PlayerEntityId = player,
                        DateExpires = ends.Value,
                        RoomEntity = null!,
                        PlayerEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Morningstar's votes are all up votes; a vote is a rating of 1.</summary>
    private static async Task VotesAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<RoomRatingEntity>("room_ratings");
        var seen = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("room_votes", null, ct).ConfigureAwait(false)
        )
        {
            var room = row.Int("room_id");
            var player = row.Int("user_id");

            if (
                !ctx.RoomOwners.ContainsKey(room)
                || !ctx.Players.Contains(player)
                || !seen.Add((room, player))
            )
                continue;

            await batch
                .AddAsync(
                    new RoomRatingEntity
                    {
                        CreatedAt = ctx.Now,
                        RoomEntityId = room,
                        PlayerEntityId = player,
                        Rating = 1,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task WordFiltersAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<RoomFilterWordEntity>("room_filter_words");
        var seen = new HashSet<(int, string)>();

        await foreach (
            var row in ctx.Source.ReadAsync("room_wordfilter", null, ct).ConfigureAwait(false)
        )
        {
            var room = row.Int("room_id");
            var word = row.Text("word").Trim();

            if (
                word.Length == 0
                || word.Length > RoomFilterWordEntity.WORD_MAX_LENGTH
                || !ctx.RoomOwners.ContainsKey(room)
                || !seen.Add((room, word.ToLowerInvariant()))
            )
                continue;

            await batch
                .AddAsync(
                    new RoomFilterWordEntity
                    {
                        CreatedAt = ctx.Now,
                        RoomEntityId = room,
                        Word = word,
                        RoomEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task FavouritesAsync(ImportContext ctx, CancellationToken ct)
    {
        var batch = ctx.Batch<PlayerFavoriteRoomsEntity>("player_favorite_rooms");
        var seen = new HashSet<(int, int)>();

        await foreach (
            var row in ctx.Source.ReadAsync("users_favorite_rooms", null, ct).ConfigureAwait(false)
        )
        {
            var player = row.Int("user_id");
            var room = row.Int("room_id");

            if (
                !ctx.Players.Contains(player)
                || !ctx.RoomOwners.ContainsKey(room)
                || !seen.Add((player, room))
            )
                continue;

            await batch
                .AddAsync(
                    new PlayerFavoriteRoomsEntity
                    {
                        CreatedAt = ctx.Now,
                        PlayerEntityId = player,
                        RoomEntityId = room,
                        PlayerEntity = null!,
                        RoomEntity = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    private static RoomDoorModeType DoorMode(string state) =>
        state.Trim().ToLowerInvariant() switch
        {
            "locked" => RoomDoorModeType.Locked,
            "password" => RoomDoorModeType.Password,
            "invisible" => RoomDoorModeType.Invisible,
            _ => RoomDoorModeType.Open,
        };

    private static ModSettingType ModSetting(int value) =>
        value switch
        {
            1 => ModSettingType.Rights,
            2 => ModSettingType.All,
            _ => ModSettingType.Owner,
        };

    private static RoomThicknessType Thickness(int value) =>
        value is >= -2 and <= 1 ? (RoomThicknessType)value : RoomThicknessType.Normal;

    /// <summary>Morningstar writes "0.0" for a room that was never decorated; Turbo leaves it empty.</summary>
    private static string? Paint(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim() is "0" or "0.0" ? null : value.Trim();

    /// <summary>Morningstar separates tags with ';', Turbo with ','.</summary>
    private static string? Tags(string? value, ImportLimits limits)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var tags = RoomTags
            .Normalize(value.Split(';', ','), limits.RoomTagsMax, limits.RoomTagMaxLength)
            .ToList();

        while (tags.Count > 0 && RoomTags.Join(tags).Length > RoomEntity.TAGS_MAX_LENGTH)
            tags.RemoveAt(tags.Count - 1);

        return tags.Count == 0 ? null : RoomTags.Join(tags);
    }
}
