using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// G2 behaviour replay, movement first: rooms as official client captures entered them, and every
/// furni move a WiredMovements told there, made again in ours.
/// <para>
/// Expected values: Fixtures/official-wired-movements.json, one entry per room entry of a capture
/// (build and recording time on it): the model, the HeightMap Habbo sent on entry, every floor
/// furni of its Objects, then in time order what changed the furni (ObjectAdd, ObjectUpdate,
/// ObjectRemove) and each WiredMovements with the HeightMapUpdates of its tick. It is generated
/// from the recordings, never edited by hand. The boxes behind a move are not recorded (other
/// users' rooms): a move is made again by a Move and Rotate Furni per furni, its arrow the
/// recorded step and its turn the recorded rotation, all fired at once by one click, so what is
/// compared is where the room puts the furni (altitude, tile, turn, animation time) and the
/// stacking heights it tells the client of the tiles the furni left and took, not which box moved
/// it. Users are not stood in the room (a recorded move was not blocked by one).
/// </para>
/// </summary>
public sealed class WiredOfficialMovementReplayTests
{
    private const string FIXTURE = "Rooms.Fixtures.official-wired-movements.json";
    private const int CLICK_ME = 900_000;
    private const int TRIGGER = 900_001;
    private const int IN_ORDER = 900_002;
    private const int FIRST_MOVER = 900_100;
    private const int CLICKER = 899_999;
    private const double Z_TOLERANCE = 0.0001;

    private static readonly Lazy<(
        List<JsonElement> Recordings,
        Dictionary<int, JsonElement> Types
    )> Fixture = new(() => RecordedRoom.Load(FIXTURE));

    /// <summary>
    /// How many differences each room entry had when the replay was written (tiles on entry, moves
    /// and heights after them), every one listed by the failure message and in the round report
    /// (B r58 golden2): a fix that removes some lowers its count, a change that adds one fails.
    /// Keyed "recording/segment".
    /// </summary>
    private static readonly Dictionary<string, (int Load, int Move)> KnownDifferences = new()
    {
        ["2026-10-14T09-23-50/0"] = (37, 0),
        ["2026-10-17T00-31-23/0"] = (20, 0),
        ["2026-10-17T18-26-25/0"] = (15, 0),
        ["2026-10-17T22-42-42/0"] = (2, 21),
        ["2026-10-17T22-43-11/0"] = (29, 9),
        ["2026-10-17T22-43-24/0"] = (15, 2),
        ["2026-10-17T22-43-51/0"] = (15, 0),
        ["2026-10-17T22-44-37/0"] = (35, 0),
        ["2026-10-17T22-45-15/0"] = (15, 0),
        ["2026-10-18T08-39-04/0"] = (15, 8),
        ["2026-10-18T08-39-04/1"] = (98, 0),
        ["2026-10-18T08-39-04/2"] = (37, 17),
        ["2026-10-18T08-39-04/3"] = (20, 0),
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> RoomEntries =>
        [
            .. Fixture.Value.Recordings.Select(r =>
                $"{r.GetProperty("recording").GetString()}/{r.GetProperty("segment").GetInt32()}"
            ),
        ];

    [Theory]
    [MemberData(nameof(RoomEntries))]
    public void A_recorded_room_loads_with_the_stacking_heights_Habbo_sent(string entry)
    {
        var recorded = Load(entry);
        var ours = recorded.Room.Harness.State.TileEncodedHeights;
        var diffs = new List<string>();
        var hidden = 0;

        for (var idx = 0; idx < recorded.HeightMap.Length; idx++)
        {
            if (ours[idx] == recorded.HeightMap[idx])
                continue;

            // A switched-on Room Area Hider's tiles: no tile to the client, its own feature.
            if (recorded.IsHiddenByArea(idx))
            {
                hidden++;
                continue;
            }

            var (x, y) = (idx % recorded.Width, idx / recorded.Width);
            var top = recorded.Room.Map.TryGetHighestFloorItem(idx, out var item)
                ? item.Definition.Name
                : "floor";

            diffs.Add(
                $"{x},{y} {top}: official {Show(recorded.HeightMap[idx])}, ours {Show(ours[idx])}"
            );
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{entry}: {recorded.HeightMap.Length} tiles, {hidden} hidden by area hiders (not compared), {diffs.Count} differ"
        );
        diffs.Should().HaveCount(Known(entry).Load, string.Join(Environment.NewLine, diffs));
    }

    [Theory]
    [MemberData(nameof(RoomEntries))]
    public async Task Every_recorded_furni_move_lands_where_Habbo_put_it(string entry)
    {
        var recorded = Load(entry);
        var room = recorded.Room;
        var wired = room.Harness.Module<RoomWiredSystem>();
        var replay = new Replay(recorded, wired);

        await replay.BuildStackAsync(MaxMoves(entry));

        foreach (
            var evt in Fixture
                .Value.Recordings.First(r => Key(r) == entry)
                .GetProperty("events")
                .EnumerateArray()
        )
        {
            switch (evt[0].GetString())
            {
                case "set":
                    recorded.Set(evt, 2);
                    break;
                case "remove":
                    recorded.Remove(evt[2].GetInt32());
                    break;
                case "slide":
                    if (room.Harness.State.ItemsById.TryGetValue(evt[2].GetInt32(), out var slid))
                        room.Map.MoveFloorItem(
                            (IRoomFloorItem)slid,
                            room.Map.ToIdx(evt[3].GetInt32(), evt[4].GetInt32()),
                            Altitude.FromValue(evt[5].GetDouble())
                        );
                    break;
                case "move":
                    await replay.MoveAsync(evt[1].GetInt64(), evt[2], evt[3]);
                    break;
            }
        }

        TestContext.Current.TestOutputHelper?.WriteLine(replay.Summary(entry));
        replay
            .Diffs.Should()
            .HaveCount(Known(entry).Move, string.Join(Environment.NewLine, replay.Diffs));
    }

    /// <summary>The click stack the moves are made by, and what came of each recorded tick.</summary>
    private sealed class Replay(RecordedRoom recorded, RoomWiredSystem wired)
    {
        private readonly WiredRoom _room = recorded.Room;
        private int _movers;
        private long _now = 10_000;

        public List<string> Diffs { get; } = [];
        public int Ticks { get; private set; }
        public int Checked { get; private set; }
        public int Skipped { get; private set; }
        public int InPlace { get; private set; }
        public int Drifted { get; private set; }
        public int HeightTiles { get; private set; }

        public string Summary(string entry) =>
            $"{entry}: {Ticks} ticks, {Checked} moves checked, {Skipped} not replayable (no step of one tile or turn of 90), {InPlace} in place (not replayable, box unknown), {Drifted} furni not where the capture had them before a move (set back), {HeightTiles} tile heights compared, {Diffs.Count} differences";

        /// <summary>
        /// A click trigger and <paramref name="movers"/> Move and Rotate Furni on a tile that is no
        /// floor (its height is nobody's), and a user to click.
        /// </summary>
        public async Task BuildStackAsync(int movers)
        {
            var idx = Enumerable
                .Range(0, _room.Map.Width * _room.Map.Height)
                .First(i => _room.Harness.State.TileEncodedHeights[i] == -1);
            var (x, y) = _room.Map.GetTileXY(idx);

            _room.AddFloorItem(CLICK_ME, x, y);
            _room.Enter(CLICKER, x, y);
            _room.AddBox<WiredTriggerClickFurni>(TRIGGER, x, y, "wf_trg_click_furni");
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    TRIGGER,
                    stuffIds: [CLICK_ME],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
            ).Should().BeTrue();

            // In the capture's order every time: two furni stepping onto one tile in one tick
            // otherwise land in a random order.
            _room.AddBox<WiredAddonExecuteInOrder>(IN_ORDER, x, y, "wf_xtra_exec_in_order");

            for (_movers = 0; _movers < movers; _movers++)
                _room.AddBox<WiredActionMoveRotateFurni>(
                    FIRST_MOVER + _movers,
                    x,
                    y,
                    "wf_act_move_rotate"
                );

            await wired.OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [idx],
                },
                Ct
            );
            await TickAsync(0);
        }

        /// <summary>One recorded WiredMovements: its furni moves made again, the outcome compared.</summary>
        public async Task MoveAsync(long at, JsonElement moves, JsonElement officialHeights)
        {
            var map = _room.Map;
            var planned = new List<(int Id, JsonElement Move)>();
            var box = 0;

            foreach (var move in moves.EnumerateArray())
            {
                var id = move[0].GetInt32();

                if (!_room.Harness.State.ItemsById.TryGetValue(id, out var known))
                    continue;

                var item = (IRoomFloorItem)known;
                var (sx, sy, sz) = (move[1].GetInt32(), move[2].GetInt32(), move[3].GetDouble());
                var (tx, ty, rot) = (move[4].GetInt32(), move[5].GetInt32(), move[7].GetInt32());

                if (item.X != sx || item.Y != sy || Math.Abs(item.Z.Value - sz) > Z_TOLERANCE)
                {
                    Drifted++;
                    map.MoveFloorItem(item, map.ToIdx(sx, sy), Altitude.FromValue(sz));
                }

                var arrow = Arrow(tx - sx, ty - sy);
                var turn = Turn((int)item.Rotation, rot);

                if (arrow < 0 || turn < 0)
                {
                    Skipped++;
                    continue;
                }

                // In place: an altitude change (Set Altitude, physics) or an entry for a furni that
                // stays as it is; which box sent it is not recorded.
                if (arrow == 0 && turn == 0)
                {
                    InPlace++;
                    continue;
                }

                (
                    await _room.SaveAsync<UpdateActionMessage>(
                        FIRST_MOVER + box++,
                        intParams: [arrow, turn],
                        stuffIds: [id],
                        furniSources:
                        [
                            [WiredFurniSourceType.SelectedItems],
                        ],
                        definitionSpecifics: [0]
                    )
                ).Should().BeTrue();
                planned.Add((id, move));
            }

            if (planned.Count == 0)
                return;

            // The movers this tick does not use move nothing.
            for (var unused = box; unused < _movers; unused++)
                await _room.SaveAsync<UpdateActionMessage>(
                    FIRST_MOVER + unused,
                    intParams: [0, 0],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                );

            // What the furni set back did to the heights is not this tick's.
            _room.Harness.Room.TakeHeightMapUpdates();

            var before = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();

            _now = Math.Max(_now, 10_000 + at);
            await _room
                .FloorItem(CLICK_ME)
                .Logic.OnClickAsync(
                    ActionContext.CreateForPlayer((PlayerId)(100 + CLICKER), (RoomId)1),
                    0,
                    Ct
                );
            await TickAsync(3);

            var sent = _room
                .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
                .Skip(before)
                .SelectMany(p => p.FloorItems)
                .GroupBy(m => (int)m.ObjectId)
                .ToDictionary(g => g.Key, g => g.Last());
            var ourHeights = _room
                .Harness.Room.TakeHeightMapUpdates()
                .OfType<HeightMapUpdateMessageComposer>()
                .SelectMany(u => u.TileHeights)
                .ToDictionary(h => (h.X, h.Y), h => h.Height);

            Ticks++;

            var tiles = new HashSet<(int X, int Y)>();

            foreach (var (id, move) in planned)
            {
                Checked++;

                var item = _room.FloorItem(id);
                var name = $"{item.Definition.Name}#{id}";
                var where = $"t={at} {name} {move[1]},{move[2]}->{move[4]},{move[5]}";

                foreach (var tile in Footprint(item, move[1].GetInt32(), move[2].GetInt32()))
                    tiles.Add(tile);
                foreach (var tile in Footprint(item, move[4].GetInt32(), move[5].GetInt32()))
                    tiles.Add(tile);

                if (!sent.TryGetValue(id, out var ours))
                {
                    Diffs.Add($"{where}: not moved (official to z {move[6]})");
                    continue;
                }

                if (ours.TargetX != move[4].GetInt32() || ours.TargetY != move[5].GetInt32())
                    Diffs.Add(
                        $"{where}: target official {move[4]},{move[5]}, ours {ours.TargetX},{ours.TargetY}"
                    );
                if (Math.Abs(ours.TargetZ.Value - move[6].GetDouble()) > Z_TOLERANCE)
                    Diffs.Add($"{where}: targetZ official {move[6]}, ours {ours.TargetZ}");
                if (Math.Abs(ours.SourceZ.Value - move[3].GetDouble()) > Z_TOLERANCE)
                    Diffs.Add($"{where}: sourceZ official {move[3]}, ours {ours.SourceZ}");
                if ((int)ours.Rotation != move[7].GetInt32())
                    Diffs.Add($"{where}: rotation official {move[7]}, ours {(int)ours.Rotation}");
                if (ours.AnimationTime != move[8].GetInt32())
                    Diffs.Add(
                        $"{where}: animationTime official {move[8]}, ours {ours.AnimationTime}"
                    );
            }

            // The capture stopped before Habbo's next height flush: its heights are not known.
            if (officialHeights.ValueKind == JsonValueKind.Null)
                return;

            var official = new Dictionary<(int, int), short>();

            foreach (var h in officialHeights.EnumerateArray())
                official[(h[0].GetInt32(), h[1].GetInt32())] = h[2].GetInt16();

            foreach (var tile in tiles.Order())
            {
                var hasOfficial = official.TryGetValue(tile, out var o);
                var hasOurs = ourHeights.TryGetValue(tile, out var u);

                HeightTiles++;

                if (hasOfficial != hasOurs || o != u)
                    Diffs.Add(
                        $"t={at} height {tile.X},{tile.Y}: official {(hasOfficial ? Show(o) : "not sent")}, ours {(hasOurs ? Show(u) : "not sent")}"
                    );
            }
        }

        private IEnumerable<(int X, int Y)> Footprint(IRoomFloorItem item, int x, int y)
        {
            var (w, l) =
                (int)item.Rotation % 4 == 2
                    ? (item.Definition.Length, item.Definition.Width)
                    : (item.Definition.Width, item.Definition.Length);

            for (var dx = 0; dx < w; dx++)
            for (var dy = 0; dy < l; dy++)
                yield return (x + dx, y + dy);
        }

        private async Task TickAsync(int ticks)
        {
            for (var i = 0; i <= ticks; i++)
            {
                _now += 50;
                _room.Clock.AdvanceMs(50);
                await wired.ProcessWiredAsync(_now, dormant: false, Ct);
            }
        }

        /// <summary>The Move and Rotate Furni arrow (its first int) for one step; -1 for none.</summary>
        private static int Arrow(int dx, int dy) =>
            (dx, dy) switch
            {
                (0, 0) => 0,
                (0, -1) => 4,
                (1, 0) => 5,
                (0, 1) => 6,
                (-1, 0) => 7,
                (1, -1) => 8,
                (1, 1) => 9,
                (-1, 1) => 10,
                (-1, -1) => 11,
                _ => -1,
            };

        /// <summary>Its turn (second int): none, clockwise or counter-clockwise; -1 for another turn.</summary>
        private static int Turn(int from, int to) =>
            (((to - from) % 8 + 8) % 8) switch
            {
                0 => 0,
                2 => 1,
                6 => 2,
                _ => -1,
            };
    }

    private static RecordedRoom Load(string entry) =>
        new(Fixture.Value.Recordings.First(r => Key(r) == entry), Fixture.Value.Types);

    private static string Key(JsonElement r) =>
        $"{r.GetProperty("recording").GetString()}/{r.GetProperty("segment").GetInt32()}";

    private static int MaxMoves(string entry) =>
        Fixture
            .Value.Recordings.First(r => Key(r) == entry)
            .GetProperty("events")
            .EnumerateArray()
            .Where(e => e[0].GetString() == "move")
            .Select(e => e[2].GetArrayLength())
            .DefaultIfEmpty(0)
            .Max();

    private static (int Load, int Move) Known(string entry) =>
        KnownDifferences.GetValueOrDefault(entry);

    private static string Show(short encoded) =>
        encoded == -1
            ? "no tile"
            : $"{(encoded & 0x7FFF) / 256.0}{((encoded & 0x8000) != 0 ? " blocked" : "")}";
}
