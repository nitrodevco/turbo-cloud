using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Database.Entities.Furniture;
using Turbo.Import.Morningstar.Formats;
using Turbo.Import.Source;
using Turbo.Import.Target;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// <c>items</c> to <c>furniture</c>, ids kept, in one pass over the table and a second over the
/// few rows that point at others:
/// <list type="bullet">
/// <item>a placed item stays where it stood; one whose room was not copied goes back to its
/// owner's inventory, and one whose owner was not copied, in a copied room, is given to the room's
/// owner so the room looks as it did;</item>
/// <item>a marketplace listing (Morningstar's <c>user_id</c> -1) goes back to its seller;</item>
/// <item>a present and what it wraps (Morningstar's <c>user_id</c> 0 rows) become Turbo's present
/// row and the rows it holds; a disc in a jukebox's playlist (<c>room_id</c> -1) a row the jukebox
/// holds, in playlist order;</item>
/// <item>a placed wired box gets Turbo's settings from <see cref="WiredSettings"/>, and a
/// snapshot box the state of the furni it remembers.</item>
/// </list>
/// Each item's state is written by <see cref="FurnitureState"/> as its Turbo logic reads it.
/// </summary>
internal sealed class FurnitureStep : IImportStep
{
    private const string TABLE = "furniture";

    public string Name => "furniture";

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var partners = await TeleportPairsAsync(ctx, ct).ConfigureAwait(false);
        var playlist = await PlaylistAsync(ctx, ct).ConfigureAwait(false);
        var sellers = await SellersAsync(ctx, ct).ConfigureAwait(false);
        var snapshotted = await SnapshottedAsync(ctx, ct).ConfigureAwait(false);

        var batch = ctx.Batch<FurnitureEntity>(TABLE);
        var held = new Dictionary<int, SourceRow>();
        var presents = new List<Placed>();
        var discs = new List<SourceRow>();
        var wired = new List<Placed>();
        var snapshots = new Dictionary<int, SnapshotTarget>();
        var jukeboxes = new HashSet<int>();

        await foreach (var row in ctx.Source.ReadAsync("items", "id", ct).ConfigureAwait(false))
        {
            var id = row.Int("id");
            var room = row.Int("room_id");
            var owner = row.Int("user_id");

            if (id <= 0 || id >= FurniIdBands.TemporaryMin)
            {
                ctx.Report.Note(
                    "item left out: its id is in the range Turbo keeps for temporary furni",
                    $"item {id}"
                );

                continue;
            }

            if (!ctx.Definitions.TryGetValue(row.Int("item_id"), out var def))
            {
                ctx.Report.Note(
                    "item left out: its item base was not copied",
                    $"item {id} base {row.Int("item_id")}"
                );

                continue;
            }

            // In a jukebox's playlist, or wrapped in a present: written with what holds it.
            if (room == -1)
            {
                discs.Add(row.Detach());

                continue;
            }

            if (owner == 0)
            {
                held[id] = row.Detach();

                continue;
            }

            if (owner == -1)
            {
                if (!sellers.TryGetValue(id, out owner))
                {
                    ctx.Report.Note(
                        "item left out: sold on the marketplace and never collected",
                        $"item {id}"
                    );

                    continue;
                }

                room = 0;
                ctx.Report.Note(
                    "marketplace listing returned to its seller's inventory",
                    $"item {id}"
                );
            }

            if (Place(ctx, row, id, owner, room, def) is not { } placed)
                continue;

            if (snapshotted.Contains(id) && placed.Room is { } inRoom)
                snapshots[id] = new SnapshotTarget(inRoom, placed.Entity.Z, def.Id);

            if (def.Logic == "jukebox")
                jukeboxes.Add(id);

            // Kept for the second pass: the row is copied out of the reader.
            if (def.Logic == PresentData.LOGIC_NAME)
            {
                presents.Add(placed with { Row = row.Detach() });

                continue;
            }

            if (placed.Room is not null && IsWiredBox(def) && row.Text("wired_data").Length > 0)
            {
                wired.Add(placed with { Row = row.Detach() });

                continue;
            }

            if (IsUnmatchedWiredBox(def))
                ctx.Report.Note(
                    "wired box copied as furni that does nothing: Turbo has no box like it",
                    $"item {id} ({def.SourceInteraction})"
                );

            placed.Entity.ExtraData = Json(
                FurnitureState.Translate(def, State(ctx, row, def, partners))
            );

            await batch.AddAsync(placed.Entity, ct).ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);

        await PresentsAsync(ctx, presents, held, partners, ct).ConfigureAwait(false);
        await DiscsAsync(ctx, discs, playlist, jukeboxes, ct).ConfigureAwait(false);
        await WiredAsync(ctx, wired, snapshots, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// The row as Turbo will hold it, with where it stands; null for one that cannot be copied
    /// (noted). A placed item's owner may differ from the room's. The result's row is the
    /// reader's current one.
    /// </summary>
    private static Placed? Place(
        ImportContext ctx,
        SourceRow row,
        int id,
        int owner,
        int room,
        DefinitionRef def
    )
    {
        int? inRoom = room > 0 && ctx.RoomOwners.ContainsKey(room) ? room : null;

        if (room > 0 && inRoom is null)
            ctx.Report.Note(
                "item put in its owner's inventory: its room was not copied",
                $"item {id} room {room}"
            );

        if (!ctx.Players.Contains(owner))
        {
            if (inRoom is not { } r)
            {
                ctx.Report.Note(
                    "item left out: its owner was not copied",
                    $"item {id} owner {owner}"
                );

                return null;
            }

            ctx.Report.Note(
                "item given to the room's owner: its own owner was not copied",
                $"item {id} owner {owner}"
            );
            owner = ctx.RoomOwners[r];
        }

        var entity = new FurnitureEntity
        {
            Id = id,
            CreatedAt = ctx.Now,
            PlayerEntityId = owner,
            FurnitureDefinitionEntityId = def.Id,
            RoomEntityId = inRoom,
        };

        if (inRoom is not null)
        {
            if (def.Type == ProductType.Wall)
            {
                if (!WallPosition.TryParse(row.Text("wall_pos").Trim(), out var wall))
                {
                    ctx.Report.Note(
                        "wall item put in its owner's inventory: its wall position does not read",
                        $"item {id} '{row.Text("wall_pos")}'"
                    );
                    entity.RoomEntityId = null;

                    return new Placed(row, entity, def, null);
                }

                entity.X = wall.X;
                entity.Y = wall.Y;
                entity.WallOffset = wall.WallOffset;
                // A whole number: the client is sent the height as written.
                entity.Z = Math.Round(wall.Z.Value);
                entity.Rotation = wall.Rotation;
            }
            else
            {
                entity.X = row.Int("x");
                entity.Y = row.Int("y");
                entity.Z = Math.Round(Math.Clamp(row.Double("z"), -9999, 9999), 3);
                entity.Rotation = Directions.Rotation(row.Int("rot"));
            }
        }

        return new Placed(row, entity, def, entity.RoomEntityId);
    }

    private static ItemState State(
        ImportContext ctx,
        SourceRow row,
        DefinitionRef def,
        IReadOnlyDictionary<int, int> partners,
        string? product = null
    )
    {
        var guild = row.Int("guild_id");
        var room = row.Int("room_id");

        return new ItemState
        {
            ExtraData = row.Text("extra_data"),
            LimitedData = row.Text("limited_data"),
            GuildId = ctx.GuildRooms.ContainsKey(guild) ? guild : 0,
            Partner = def.Logic == "teleport" ? partners.GetValueOrDefault(row.Int("id")) : 0,
            RoomMoodlight =
                def.Logic == "dimmer" ? ctx.RoomMoodlights.GetValueOrDefault(room) : null,
            PresentProduct = product,
            NameOf = id => ctx.PlayerNames.GetValueOrDefault(id),
            SongOf = id => ctx.Songs.TryGetValue(id, out var song) ? song : null,
            Now = ctx.Now,
        };
    }

    /// <summary>
    /// Presents, after everything else, so what they wrap is known: the present row, then each
    /// wrapped row held by it. A wrapped row no present names is left out.
    /// </summary>
    private static async Task PresentsAsync(
        ImportContext ctx,
        List<Placed> presents,
        Dictionary<int, SourceRow> held,
        IReadOnlyDictionary<int, int> partners,
        CancellationToken ct
    )
    {
        var boxes = ctx.Batch<FurnitureEntity>(TABLE);
        var contents = ctx.Batch<FurnitureEntity>(TABLE).After(boxes);
        var claimed = new HashSet<int>();

        foreach (var present in presents)
        {
            var wrapped = FurnitureState
                .PresentContents(present.Row.Text("extra_data"))
                .Where(x => held.ContainsKey(x) && claimed.Add(x))
                .ToList();

            var product = wrapped
                .Select(x => ctx.Definitions.GetValueOrDefault(held[x].Int("item_id"))?.Name)
                .FirstOrDefault(x => x is not null);

            if (wrapped.Count == 0)
                ctx.Report.Note(
                    "present copied empty: what it wrapped was not found",
                    $"item {present.Entity.Id}"
                );

            present.Entity.ExtraData = Json(
                FurnitureState.Translate(
                    present.Definition,
                    State(ctx, present.Row, present.Definition, partners, product)
                )
            );

            await boxes.AddAsync(present.Entity, ct).ConfigureAwait(false);

            foreach (var id in wrapped)
            {
                var row = held[id];
                var def = ctx.Definitions[row.Int("item_id")];

                await contents
                    .AddAsync(
                        new FurnitureEntity
                        {
                            Id = id,
                            CreatedAt = ctx.Now,
                            PlayerEntityId = present.Entity.PlayerEntityId,
                            FurnitureDefinitionEntityId = def.Id,
                            ChestItemEntityId = present.Entity.Id,
                            ExtraData = Json(
                                FurnitureState.Translate(def, State(ctx, row, def, partners))
                            ),
                        },
                        ct
                    )
                    .ConfigureAwait(false);
            }
        }

        await contents.FlushAsync(ct).ConfigureAwait(false);

        foreach (var id in held.Keys.Where(x => !claimed.Contains(x)))
            ctx.Report.Note("item left out: it has no owner and no present holds it", $"item {id}");
    }

    /// <summary>Discs in a playlist: held by their jukebox, in order; else back to their owner.</summary>
    private static async Task DiscsAsync(
        ImportContext ctx,
        List<SourceRow> discs,
        IReadOnlyDictionary<int, (int Jukebox, int Position)> playlist,
        HashSet<int> jukeboxes,
        CancellationToken ct
    )
    {
        var batch = ctx.Batch<FurnitureEntity>(TABLE);

        foreach (var row in discs)
        {
            var id = row.Int("id");
            var owner = row.Int("user_id");
            var def = ctx.Definitions[row.Int("item_id")];
            var inJukebox =
                playlist.TryGetValue(id, out var slot) && jukeboxes.Contains(slot.Jukebox);

            if (!ctx.Players.Contains(owner))
            {
                ctx.Report.Note(
                    "item left out: its owner was not copied",
                    $"item {id} owner {owner}"
                );

                continue;
            }

            if (!inJukebox)
                ctx.Report.Note(
                    "music disc put in its owner's inventory: its jukebox was not copied",
                    $"item {id}"
                );

            await batch
                .AddAsync(
                    new FurnitureEntity
                    {
                        Id = id,
                        CreatedAt = ctx.Now,
                        PlayerEntityId = owner,
                        FurnitureDefinitionEntityId = def.Id,
                        ChestItemEntityId = inJukebox ? slot.Jukebox : null,
                        HeldPosition = inJukebox ? slot.Position : null,
                        ExtraData = Json(
                            FurnitureState.Translate(
                                def,
                                State(ctx, row, def, new Dictionary<int, int>())
                            )
                        ),
                    },
                    ct
                )
                .ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Placed wired boxes, last, so a snapshot box knows the furni it remembers.</summary>
    private static async Task WiredAsync(
        ImportContext ctx,
        List<Placed> boxes,
        IReadOnlyDictionary<int, SnapshotTarget> snapshots,
        CancellationToken ct
    )
    {
        var batch = ctx.Batch<FurnitureEntity>(TABLE);

        foreach (var box in boxes)
        {
            WiredResult? result;

            try
            {
                result = WiredSettings.Translate(
                    box.Definition.SourceInteraction,
                    box.Row.Text("wired_data"),
                    baseId => ctx.BaseItems.TryGetValue(baseId, out var reward) ? reward.Name : null
                );
            }
            catch (Exception ex)
                when (ex is InvalidOperationException or FormatException or JsonException)
            {
                result = null;
                ctx.Report.Note(
                    "wired box's settings left out: they do not read",
                    $"item {box.Entity.Id} ({ex.Message})"
                );
            }

            var sections = new JsonObject();

            if (result is null)
                ctx.Report.Note(
                    "wired box copied with Turbo's default settings: Turbo has no box like it",
                    $"item {box.Entity.Id} ({box.Definition.SourceInteraction})"
                );
            else
            {
                sections["wired"] = result.Wired;

                if (result.Lost is { } lost)
                    ctx.Report.Note($"wired box copied without {lost}", $"item {box.Entity.Id}");

                if (
                    Snapshot(result.Snapshot, box.Room!.Value, snapshots) is { Count: > 0 } snapshot
                )
                    sections["wired_snapshot"] = snapshot;
            }

            box.Entity.ExtraData = Json(sections.Count == 0 ? null : sections);

            await batch.AddAsync(box.Entity, ct).ConfigureAwait(false);
        }

        await batch.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Turbo's <c>wired_snapshot</c>: for each remembered furni in the box's room, its state,
    /// rotation and place as Morningstar remembered them, its height in hundredths and its definition.
    /// </summary>
    private static JsonObject? Snapshot(
        IReadOnlyList<SnapshotPick> picks,
        int room,
        IReadOnlyDictionary<int, SnapshotTarget> targets
    )
    {
        if (picks.Count == 0)
            return null;

        var snapshot = new JsonObject();

        foreach (var pick in picks)
        {
            if (!targets.TryGetValue(pick.ItemId, out var target) || target.Room != room)
                continue;

            snapshot[pick.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture)] =
                new JsonObject
                {
                    ["state"] = int.TryParse(pick.State.Trim(), out var state) ? state : 0,
                    ["rotation"] = Math.Clamp(pick.Rotation, 0, 7),
                    ["x"] = pick.X,
                    ["y"] = pick.Y,
                    ["z"] = (int)Math.Round(target.Z * 100),
                    ["definitionId"] = target.DefinitionId,
                };
        }

        return snapshot;
    }

    private static bool IsWiredBox(DefinitionRef def) =>
        def.Logic.StartsWith("wf_", StringComparison.Ordinal)
        && def.SourceInteraction.StartsWith("wf_", StringComparison.Ordinal);

    // wf_blob and wf_highscore are furni with wired names, not boxes.
    private static bool IsUnmatchedWiredBox(DefinitionRef def) =>
        def.SourceInteraction.StartsWith("wf_", StringComparison.Ordinal)
        && def.SourceInteraction is not ("wf_blob" or "wf_highscore")
        && !def.Logic.StartsWith("wf_", StringComparison.Ordinal);

    private static string? Json(JsonObject? sections) => sections?.ToJsonString();

    private static async Task<Dictionary<int, int>> TeleportPairsAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var partners = new Dictionary<int, int>();

        await foreach (
            var row in ctx.Source.ReadAsync("items_teleports", null, ct).ConfigureAwait(false)
        )
        {
            var one = row.Int("teleport_one_id");
            var two = row.Int("teleport_two_id");

            if (one <= 0 || two <= 0 || one == two)
                continue;

            partners.TryAdd(one, two);
            partners.TryAdd(two, one);
        }

        return partners;
    }

    /// <summary>Each disc in a playlist: its jukebox and its place in the playlist, from 0.</summary>
    private static async Task<Dictionary<int, (int Jukebox, int Position)>> PlaylistAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var playlist = new Dictionary<int, (int, int)>();
        var counts = new Dictionary<int, int>();

        await foreach (
            var row in ctx.Source.ReadAsync("trax_playlist", null, ct).ConfigureAwait(false)
        )
        {
            var jukebox = row.Int("trax_item_id");
            var disc = row.Int("item_id");
            var position = counts.GetValueOrDefault(jukebox);

            if (playlist.TryAdd(disc, (jukebox, position)))
                counts[jukebox] = position + 1;
        }

        return playlist;
    }

    /// <summary>The seller of each item still listed on the marketplace.</summary>
    private static async Task<Dictionary<int, int>> SellersAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var sellers = new Dictionary<int, int>();

        await foreach (
            var row in ctx.Source.ReadAsync("marketplace_items", "id", ct).ConfigureAwait(false)
        )
            if (row.Int("state") == 1 && row.Int("user_id") > 0)
                sellers[row.Int("item_id")] = row.Int("user_id");

        return sellers;
    }

    /// <summary>The furni some snapshot box remembers: read before the items, so their places are kept.</summary>
    private static async Task<HashSet<int>> SnapshottedAsync(
        ImportContext ctx,
        CancellationToken ct
    )
    {
        var ids = new HashSet<int>();

        if (!ctx.Source.HasColumn("items", "wired_data"))
            return ids;

        await foreach (
            var row in ctx
                .Source.ReadWhereAsync(
                    "items",
                    "wired_data IS NOT NULL AND wired_data <> ''",
                    "id",
                    ct
                )
                .ConfigureAwait(false)
        )
        {
            if (!ctx.BaseItems.TryGetValue(row.Int("item_id"), out var item))
                continue;

            if (
                item.Interaction
                is not (
                    "wf_act_match_to_sshot"
                    or "wf_cnd_match_snapshot"
                    or "wf_cnd_not_match_snap"
                )
            )
                continue;

            try
            {
                var result = WiredSettings.Translate(item.Interaction, row.Text("wired_data"), _ =>
                    null
                );

                foreach (var pick in result?.Snapshot ?? [])
                    ids.Add(pick.ItemId);
            }
            catch (Exception ex)
                when (ex is InvalidOperationException or FormatException or JsonException)
            {
                // Said once, when the box itself is written.
            }
        }

        return ids;
    }

    private sealed record Placed(
        SourceRow Row,
        FurnitureEntity Entity,
        DefinitionRef Definition,
        int? Room
    );

    private sealed record SnapshotTarget(int Room, double Z, int DefinitionId);
}
