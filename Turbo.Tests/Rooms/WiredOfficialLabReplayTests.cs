using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// G2 behaviour replay beyond movement: official wired lab sessions made again in ours, and what
/// Habbo answered compared with what ours answers, for selectors, variables and chests.
/// <para>
/// Expected values: Fixtures/official-wired-lab-replay.json, one entry per room entry of a lab
/// session in our own official room (build and recording time on it): the model, the floor furni
/// at entry (wired boxes by class), the users, the saves that configured the boxes before the
/// entry (the last save or editor Open of each), then in time order what the lab sent (saves,
/// clicks, uses, variable reads), what the room put there itself (placements, pickups, hand moves,
/// where users stood) and what Habbo answered: Show Message whispers, box flashes (which boxes
/// ran) and WiredVariablesForObject values of the lab's own variables (by name). It is generated
/// from the recordings, never edited by hand.
/// </para>
/// <para>
/// The session is cut into windows, one per click, use or save. Per window the whispers, the
/// variable values read and whether the save was taken are compared as multisets, so the order
/// inside a tick and a few ms of timing do not count. Box flashes only say which boxes ran (they
/// put the window in an area): a flash's state and batching are their own matter. A window with a send
/// the replay does not make (a walk, a bot, a chest, a chat line) is not compared; the room goes on.
/// A window belongs to an area (selectors, variables, chests) when a box of it ran or was saved in
/// the window, or a variable was read.
/// </para>
/// </summary>
public sealed class WiredOfficialLabReplayTests
{
    private const string FIXTURE = "Rooms.Fixtures.official-wired-lab-replay.json";
    private const int TICK_MS = 50;

    private static readonly Lazy<(
        List<JsonElement> Recordings,
        Dictionary<int, JsonElement> Types
    )> Fixture = new(() => RecordedRoom.Load(FIXTURE));

    private static readonly ConcurrentDictionary<string, Task<Outcome>> Replays = new();

    /// <summary>
    /// How many differences each room entry and area had when the replay was written, every one
    /// listed by the failure message: a fix that removes some lowers its count, a change that adds
    /// one fails. Keyed "session/segment/area".
    /// <para>
    /// Most are the replay's, not the room's: bots are not stood in the room (whispers naming
    /// them, bot boxes), furni carry no public name (furni name text output shows the sprite), and
    /// a window's state can follow an earlier window the replay skipped. The rest are listed per
    /// fixture entry where the wired matrix keeps ours-versus-Habbo differences (r58 golden3).
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, int> KnownDifferences = new()
    {
        ["wlab10/1/chest"] = 2,
        ["wlab10/2/chest"] = 18,
        ["wlab10/2/selector"] = 15,
        ["wlab10/3/selector"] = 4,
        ["wlab11/1/selector"] = 2,
        ["wlab5/0/selector"] = 10,
        ["wlab5/0/variable"] = 3,
        ["wlab6/1/selector"] = 10,
        ["wlab6/1/variable"] = 10,
        ["wlab6/2/selector"] = 10,
        ["wlab6/2/variable"] = 11,
        ["wlab6/3/selector"] = 19,
        ["wlab6/3/variable"] = 19,
        ["wlab6/4/variable"] = 10,
        ["wlab7/0/selector"] = 1,
        ["wlab7/0/variable"] = 1,
        ["wlab7/1/chest"] = 12,
        ["wlab7/1/selector"] = 5,
        ["wlab7/2/chest"] = 1,
        ["wlab7/2/selector"] = 25,
        ["wlab7/2/variable"] = 12,
        ["wlab8/0/selector"] = 12,
        ["wlab8/0/variable"] = 25,
        ["wlab8/1/selector"] = 8,
        ["wlab8/1/variable"] = 10,
        ["wlab9/0/selector"] = 34,
        ["wlab9/0/variable"] = 54,
        ["wlabbuy/0/chest"] = 12,
        ["wlabduo/0/chest"] = 12,
        ["wlabduo/0/selector"] = 35,
        ["wlabduo/0/variable"] = 23,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> RoomEntries =>
        [
            .. Fixture.Value.Recordings.Select(r =>
                $"{r.GetProperty("session").GetString()}/{r.GetProperty("segment").GetInt32()}"
            ),
        ];

    [Theory]
    [MemberData(nameof(RoomEntries))]
    public Task Selectors_pick_what_Habbo_picks(string entry) => CompareAsync(entry, "selector");

    [Theory]
    [MemberData(nameof(RoomEntries))]
    public Task Variables_hold_what_Habbo_holds(string entry) => CompareAsync(entry, "variable");

    [Theory]
    [MemberData(nameof(RoomEntries))]
    public Task Chests_and_transactions_answer_as_Habbo_does(string entry) =>
        CompareAsync(entry, "chest");

    private static async Task CompareAsync(string entry, string area)
    {
        // One replay per room entry for the three areas (xUnit runs a class's cases one at a time).
        var outcome = await Replays.GetOrAdd(entry, ReplayAsync);
        var windows = outcome.Windows.Where(w => w.Areas.Contains(area)).ToList();
        var diffs = windows.Where(w => !w.Skipped).SelectMany(w => w.Diffs).ToList();

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{entry} {area}: {windows.Count} windows, {windows.Count(w => w.Skipped)} not compared (a send the replay does not make), {windows.Where(w => !w.Skipped).Sum(w => w.OfficialCount)} official outcomes compared, {diffs.Count} differences"
        );
        diffs
            .Should()
            .HaveCount(
                KnownDifferences.GetValueOrDefault($"{entry}/{area}"),
                string.Join(Environment.NewLine, diffs)
            );
    }

    private sealed record Outcome(List<Window> Windows);

    /// <summary>What happened from one click, use or save to the next, official and ours.</summary>
    private sealed class Window(long at)
    {
        public long At { get; } = at;
        public HashSet<string> Areas { get; } = [];
        public bool Skipped { get; set; }
        public List<string> Official { get; } = [];
        public List<string> Ours { get; } = [];
        public List<string> Diffs { get; } = [];

        public int OfficialCount => Official.Count;

        public void Compare()
        {
            foreach (
                var kind in Official
                    .Concat(Ours)
                    .Select(x => x[..x.IndexOf(' ')])
                    .Distinct()
                    .Order()
            )
            {
                var o = Official.Where(x => x.StartsWith(kind + " ")).Order().ToList();
                var u = Ours.Where(x => x.StartsWith(kind + " ")).Order().ToList();

                if (!o.SequenceEqual(u))
                    Diffs.Add(
                        $"t={At} {kind}: official [{string.Join(", ", o.Select(x => x[(kind.Length + 1)..]))}], ours [{string.Join(", ", u.Select(x => x[(kind.Length + 1)..]))}]"
                    );
            }
        }
    }

    private static async Task<Outcome> ReplayAsync(string entry)
    {
        var json = Fixture.Value.Recordings.First(r => Key(r) == entry);
        var recorded = new RecordedRoom(json, Fixture.Value.Types, liveBoxes: true);
        var room = recorded.Room;
        var wired = room.Harness.Module<RoomWiredSystem>();
        var replay = new Replay(recorded, wired, json);

        await replay.RunAsync();

        return new Outcome(replay.Windows);
    }

    private sealed class Replay(RecordedRoom recorded, RoomWiredSystem wired, JsonElement json)
    {
        private readonly WiredRoom _room = recorded.Room;
        private readonly Dictionary<int, string> _classOf = [];
        private int _self = -1;
        private int _whispersSeen;
        private int _flashesSeen;
        private long _t;

        public List<Window> Windows { get; } = [new Window(0)];

        private Window Current => Windows[^1];

        public async Task RunAsync()
        {
            foreach (var item in json.GetProperty("items").EnumerateArray())
                _classOf[item[0].GetInt32()] = Fixture
                    .Value.Types[item[1].GetInt32()][0]
                    .GetString()!;

            foreach (var user in json.GetProperty("users").EnumerateArray())
                Enter(
                    user[0].GetInt32(),
                    user[1].GetInt32(),
                    user[2].GetInt32(),
                    user[4].GetString()!
                );

            if (json.TryGetProperty("self", out var self) && self.ValueKind == JsonValueKind.Number)
                _self = self.GetInt32();

            await StacksChangedAsync(_classOf.Keys.Where(IsBox));

            foreach (var save in json.GetProperty("setup").EnumerateArray())
                await SaveAsync(save, 0);

            TakeOurs();
            Windows.Clear();
            Windows.Add(new Window(0));

            foreach (var evt in json.GetProperty("events").EnumerateArray())
            {
                var at = evt[0].GetInt64();

                await TickToAsync(at);

                switch (evt[1].GetString())
                {
                    case "save":
                    case "refused":
                        NewWindow(at);
                        Current.Areas.Add(Area(_classOf.GetValueOrDefault(evt[3].GetInt32(), "")));
                        Current.Official.Add(
                            $"save {evt[3]}:{(evt[1].GetString() == "save" ? "taken" : "refused")}"
                        );
                        await SaveAsync(evt, 2);
                        break;
                    case "click":
                        NewWindow(at);
                        await _room.Harness.Room.ClickItemByIdAsync(Actor(), evt[2].GetInt32(), Ct);
                        break;
                    case "use":
                        NewWindow(at);
                        await _room.Harness.Room.UseItemByIdAsync(
                            Actor(),
                            evt[2].GetInt32(),
                            Ct,
                            evt[3].GetInt32()
                        );
                        break;
                    case "other":
                        Current.Skipped = true;
                        break;
                    case "set":
                    {
                        var id = evt[2].GetInt32();
                        var known = _room.Harness.State.ItemsById.ContainsKey(id);

                        _classOf[id] = Fixture.Value.Types[evt[3].GetInt32()][0].GetString()!;
                        recorded.Set(evt, 2);
                        if (!known && IsBox(id))
                            await StacksChangedAsync([id]);
                        break;
                    }
                    case "remove":
                    {
                        var id = evt[2].GetInt32();

                        if (IsBox(id) && _room.Harness.State.ItemsById.TryGetValue(id, out var box))
                        {
                            var idx = _room.Map.ToIdx(box.X, box.Y);

                            recorded.Remove(id);
                            await StackChangedAsync(idx);
                        }
                        else
                            recorded.Remove(id);
                        break;
                    }
                    case "user":
                    case "at":
                    {
                        var index = evt[2].GetInt32();

                        if (_room.Avatars.TryGetValue(index, out var avatar))
                            avatar.SetPosition(evt[3].GetInt32(), evt[4].GetInt32());
                        else
                            Enter(index, evt[3].GetInt32(), evt[4].GetInt32(), evt[6].GetString()!);
                        break;
                    }
                    case "leave":
                        if (_room.Avatars.Remove(evt[2].GetInt32(), out var left))
                        {
                            _room.Map.RemoveAvatar(left, false);
                            _room.Harness.State.AvatarsByObjectId.Remove(left.ObjectId);
                            _room.Harness.State.AvatarsByPlayerId.Remove(left.PlayerId);
                        }
                        break;
                    case "whisper":
                        Current.Official.Add($"whisper {evt[3].GetString()}");
                        break;
                    case "flash":
                    {
                        var id = evt[2].GetInt32();

                        Current.Areas.Add(Area(_classOf.GetValueOrDefault(id, "")));
                        break;
                    }
                    case "read":
                        Current.Areas.Add("variable");
                        Current.Ours.AddRange(
                            await ValuesAsync(evt[2].GetInt32(), evt[3].GetInt32())
                        );
                        break;
                    case "values":
                        foreach (var value in evt[3].EnumerateObject())
                            if (value.Name.StartsWith('='))
                                Current.Official.Add($"value {value.Name[1..]}={value.Value}");
                        break;
                }
            }

            await TickToAsync(_t + 3_000);
            NewWindow(_t);
            Windows.RemoveAt(Windows.Count - 1);
        }

        /// <summary>A user by the alias the fixture gives its account (texts and saves name it so).</summary>
        private void Enter(int index, int x, int y, string alias) =>
            _room.Enter(index, x, y, name: alias);

        private ActionContext Actor() =>
            _room.ContextFor(
                _self >= 0 && _room.Avatars.ContainsKey(_self) ? _self : _room.Avatars.Keys.Min()
            );

        private bool IsBox(int id) => _classOf.GetValueOrDefault(id, "").StartsWith("wf_");

        private void NewWindow(long at)
        {
            TakeOurs();
            Current.Compare();
            Windows.Add(new Window(at));
        }

        /// <summary>What ours sent since the last look: whispers, and the areas of the boxes it flashed.</summary>
        private void TakeOurs()
        {
            var whispers = _room.Harness.Fakes.Sent<WhisperMessageComposer>().ToList();

            foreach (var w in whispers.Skip(_whispersSeen))
                Current.Ours.Add($"whisper {w.Text}");
            _whispersSeen = whispers.Count;

            var flashes = _room
                .Harness.Fakes.SentToRoom<ObjectsDataUpdateMessageComposer>()
                .SelectMany(u => u.StuffDatas.Select(s => (int)s.Item1))
                .Concat(
                    _room
                        .Harness.Fakes.SentToRoom<ObjectDataUpdateMessageComposer>()
                        .Select(u => (int)u.ObjectId)
                )
                .ToList();

            foreach (var id in flashes.Skip(_flashesSeen).Where(IsBox))
            {
                Current.Areas.Add(Area(_classOf[id]));
            }
            _flashesSeen = flashes.Count;
        }

        private async Task TickToAsync(long at)
        {
            while (_t + TICK_MS <= at)
            {
                _t += TICK_MS;
                _room.Clock.AdvanceMs(TICK_MS);
                await wired.ProcessWiredAsync(_room.Harness.NowMs(), dormant: false, Ct);
            }
        }

        private async Task StacksChangedAsync(IEnumerable<int> boxes)
        {
            foreach (
                var idx in boxes
                    .Select(id => _room.Harness.State.ItemsById[id])
                    .Select(b => _room.Map.ToIdx(b.X, b.Y))
                    .Distinct()
            )
                await StackChangedAsync(idx);
        }

        private Task StackChangedAsync(int idx) =>
            wired.OnRoomEventAsync(
                new RoomWiredStackChangedEvent
                {
                    RoomId = 1,
                    CausedBy = ActionContext.CreateForSystem(1),
                    StackIds = [idx],
                },
                Ct
            );

        /// <summary>A save as the lab sent it (<c>[kind, box, ints, string, stuff, definition, furni sources, user sources, variables, type, stuff2]</c> from <paramref name="at"/>).</summary>
        private async Task SaveAsync(JsonElement save, int at)
        {
            var kind = save[at].GetString()!;
            var id = save[at + 1].GetInt32();

            if (!_room.Harness.State.ItemsById.ContainsKey(id))
                return;

            var type = kind switch
            {
                "UpdateTrigger" => typeof(UpdateTriggerMessage),
                "UpdateAction" => typeof(UpdateActionMessage),
                "UpdateCondition" => typeof(UpdateConditionMessage),
                "UpdateSelector" => typeof(UpdateSelectorMessage),
                "UpdateAddon" => typeof(UpdateAddonMessage),
                _ => typeof(UpdateVariableMessage),
            };
            var message = (UpdateWiredMessage)Activator.CreateInstance(type)! with
            {
                Id = id,
                IntParams = [.. save[at + 2].EnumerateArray().Select(x => x.GetInt32())],
                StringParam = save[at + 3].GetString()!,
                StuffIds = [.. save[at + 4].EnumerateArray().Select(x => x.GetInt32())],
                DefinitionSpecifics = [.. save[at + 5].EnumerateArray().Select(Specific)],
                FurniSources =
                [
                    .. save[at + 6]
                        .EnumerateArray()
                        .Select(x =>
                            new[]
                            {
                                WiredFurniSourceTypeExtensions.FromProtocolId(
                                    (WiredSourceType)x.GetInt32()
                                ),
                            }
                        ),
                ],
                PlayerSources =
                [
                    .. save[at + 7]
                        .EnumerateArray()
                        .Select(x =>
                            new[]
                            {
                                WiredPlayerSourceTypeExtensions.FromProtocolId(
                                    (WiredSourceType)x.GetInt32()
                                ),
                            }
                        ),
                ],
                VariableIds =
                [
                    .. save[at + 8].EnumerateArray().Select(x => VariableId(x.GetString()!)),
                ],
                TypeSpecifics = [.. save[at + 9].EnumerateArray().Select(Specific)],
                StuffIds2 = [.. save[at + 10].EnumerateArray().Select(x => x.GetInt32())],
            };

            var result = await _room.Harness.Room.ApplyWiredUpdateAsync(
                ActionContext.CreateForSystem(1),
                id,
                message,
                CancellationToken.None
            );

            if (at > 0)
                Current.Ours.Add($"save {id}:{(result.IsSaved ? "taken" : "refused")}");
        }

        private static object Specific(JsonElement x) =>
            x.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => x.GetString()!,
                _ => x.GetInt32(),
            };

        /// <summary>A variable the lab made, by its name; Habbo's own (negative) ids as sent.</summary>
        private string VariableId(string sent)
        {
            if (!sent.StartsWith('=') && !sent.StartsWith('?'))
                return sent;

            var name = sent[1..];

            return wired
                    .GetAllVariables()
                    .Select(v => v.GetVarSnapshot())
                    .FirstOrDefault(v => v.VariableName == name)
                    ?.VariableId.ToString()
                ?? "0";
        }

        /// <summary>Ours for a WiredGetVariablesForObject: the lab's own variables, by name.</summary>
        private async Task<IEnumerable<string>> ValuesAsync(int type, int id)
        {
            var names = wired
                .GetAllVariables()
                .Select(v => v.GetVarSnapshot())
                .Where(v => !v.VariableName.StartsWith('@') && !v.VariableName.StartsWith('~'))
                .ToDictionary(v => v.VariableId, v => v.VariableName);
            var values = await wired.GetAllVariablesForBindingAsync(
                new WiredVariableBinding((WiredVariableTargetType)type, Math.Abs(id)),
                Ct
            );

            return values
                .Where(v => names.ContainsKey(v.id))
                .Select(v => $"value {names[v.id]}={v.value.Value}");
        }
    }

    private static readonly (string Area, Regex Classes)[] AreaClasses =
    [
        ("chest", new Regex("chest|transaction|contract|currency|wallet")),
        ("variable", new Regex("^wf_(var_|.*_var(_|$)|.*var$)")),
        ("selector", new Regex("^wf_slc_")),
    ];

    private static string Area(string classname) =>
        AreaClasses.FirstOrDefault(a => a.Classes.IsMatch(classname)).Area ?? "other";

    private static string Key(JsonElement r) =>
        $"{r.GetProperty("session").GetString()}/{r.GetProperty("segment").GetInt32()}";
}
