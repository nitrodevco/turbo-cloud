using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A freshly placed wired box opens with the editor reply Habbo's server sends for a box of the
/// same kind, field by field: the code, the furni limit, wall furni, advanced mode, the allowed
/// sources of every slot in the order sent, the default sources, the default ints, a condition's
/// quantifier type and invert flag, and what a box nobody saved holds (ints, sources, text,
/// variable slots, no quantifier, delay or selector options).
/// <para>
/// Expected values: Fixtures/official-wired-editor-opens.json, one line per box type, each the
/// newest official client capture of that box's editor open (build and recording time on the
/// line). It is generated from the recordings, never edited by hand; a new recording regenerates
/// it. Ours is the reply the Open handler sends (the room's box snapshot through the revision's
/// serializer), read back in the order the client reads it (AS3 avatar/actions/Triggerable.as:43-102,
/// ConditionDefinition.as:18-27, SelectorDefinition.as:17-21, DelayInPulsesActionDefinition.as:14-17).
/// </para>
/// </summary>
public sealed class WiredOfficialEditorContractTests
{
    private const int BOX = 2;

    private static readonly Lazy<Dictionary<string, JsonElement>> Fixture = new(Load);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> OfficialBoxes => [.. Fixture.Value.Keys];

    [Theory]
    [MemberData(nameof(OfficialBoxes))]
    public async Task A_fresh_box_opens_as_Habbo_sends_it(string classname)
    {
        var official = Fixture.Value[classname];
        var room = new WiredRoom(8, 8);

        AddBox(room, classname);

        var snapshot = await room
            .Harness.Module<RoomWiredSystem>()
            .GetBoxSnapshotAsync((RoomObjectId)BOX, Ct);
        var bytes = Sent(Composer(snapshot!));
        var ours = Read(snapshot!.WiredType, bytes);

        // The default ints go last, after the editor's variable context.
        ours["defaultIntParams"] = snapshot.DefaultIntParams;
        bytes
            .AsSpan()
            .EndsWith(Ints([snapshot.DefaultIntParams.Count, .. snapshot.DefaultIntParams]))
            .Should()
            .BeTrue();

        Differences(official, ours)
            .Should()
            .BeEquivalentTo(
                KnownDifferences.GetValueOrDefault(classname, []),
                "a fixed difference leaves KnownDifferences"
            );
    }

    /// <summary>
    /// Where ours still differs, each named until it is fixed; the test fails once one is, so
    /// the entry goes with the fix.
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownDifferences = new();

    /// <summary>The fields the fixture holds that ours does not match: "field: official X, ours Y".</summary>
    private static List<string> Differences(JsonElement official, Dictionary<string, object?> ours)
    {
        var expected = new Dictionary<string, JsonElement>();

        foreach (var name in TypeFacts)
            if (official.TryGetProperty(name, out var value))
                expected[name] = value;

        // What a box nobody saved holds; known when Habbo was recorded opening one.
        if (official.TryGetProperty("freshRecording", out _))
        {
            expected["intParams"] = official.GetProperty("defaultIntParams");
            expected["furniSourceTypes"] = official.GetProperty("defaultFurniSources");
            expected["userSourceTypes"] = official.GetProperty("defaultUserSources");
            expected["stringParam"] = official.GetProperty("freshString");
            expected["variableIds"] = official.GetProperty("freshVariableIds");
            expected["quantifierCode"] = JsonSerializer.SerializeToElement(0);
            expected["delayInPulses"] = JsonSerializer.SerializeToElement(0);
            expected["isFilter"] = JsonSerializer.SerializeToElement(false);
            expected["selectorInvert"] = JsonSerializer.SerializeToElement(false);
        }

        return
        [
            .. expected
                .Where(x => ours.ContainsKey(x.Key) || x.Key is "quantifierType" or "isInvert")
                .Select(x =>
                    (
                        x.Key,
                        Official: JsonSerializer.Serialize(x.Value),
                        Ours: JsonSerializer.Serialize(ours.GetValueOrDefault(x.Key))
                    )
                )
                .Where(x => x.Official != x.Ours)
                .Select(x => $"{x.Key}: official {x.Official}, ours {x.Ours}"),
        ];
    }

    private static readonly string[] TypeFacts =
    [
        "holder",
        "code",
        "furniLimit",
        "allowWallFurni",
        "advancedMode",
        "defaultIntParams",
        "allowedFurniSources",
        "allowedUserSources",
        "defaultFurniSources",
        "defaultUserSources",
        "quantifierType",
        "isInvert",
    ];

    /// <summary>The editor reply as the client reads it, up to the variable context.</summary>
    private static Dictionary<string, object?> Read(WiredType type, byte[] bytes)
    {
        var p = new ClientPacket(0, bytes);
        var ours = new Dictionary<string, object?>
        {
            ["holder"] = type.ToString().ToLowerInvariant(),
            ["furniLimit"] = p.PopInt(),
        };

        IntList(p);
        IntList(p);
        p.PopInt();
        p.PopInt();
        ours["stringParam"] = p.PopString();
        ours["intParams"] = IntList(p);
        ours["variableIds"] = Enumerable.Range(0, p.PopInt()).Select(_ => p.PopString()).ToList();
        ours["furniSourceTypes"] = IntList(p);
        ours["userSourceTypes"] = IntList(p);
        ours["code"] = p.PopInt();

        switch (type)
        {
            case WiredType.Action:
                ours["delayInPulses"] = p.PopInt();
                break;
            case WiredType.Condition:
                ours["quantifierCode"] = p.PopInt();
                break;
            case WiredType.Selector:
                ours["isFilter"] = p.PopBoolean();
                ours["selectorInvert"] = p.PopBoolean();
                break;
        }

        ours["advancedMode"] = p.PopBoolean();
        ours["allowedFurniSources"] = Enumerable
            .Range(0, p.PopInt())
            .Select(_ => IntList(p))
            .ToList();
        ours["allowedUserSources"] = Enumerable
            .Range(0, p.PopInt())
            .Select(_ => IntList(p))
            .ToList();
        ours["defaultFurniSources"] = IntList(p);
        ours["defaultUserSources"] = IntList(p);
        ours["allowWallFurni"] = p.PopBoolean();

        if (type == WiredType.Condition)
        {
            ours["quantifierType"] = (int)p.PopByte();
            ours["isInvert"] = p.PopBoolean();
        }

        return ours;
    }

    private static List<int> IntList(ClientPacket p) =>
        [.. Enumerable.Range(0, p.PopInt()).Select(_ => p.PopInt())];

    private static IComposer Composer(WiredDataSnapshot snapshot) =>
        snapshot.WiredType switch
        {
            WiredType.Action => new WiredFurniActionEventMessageComposer { WiredData = snapshot },
            WiredType.Addon => new WiredFurniAddonEventMessageComposer { WiredData = snapshot },
            WiredType.Condition => new WiredFurniConditionEventMessageComposer
            {
                WiredData = snapshot,
            },
            WiredType.Selector => new WiredFurniSelectorEventMessageComposer
            {
                WiredData = snapshot,
            },
            WiredType.Trigger => new WiredFurniTriggerEventMessageComposer { WiredData = snapshot },
            _ => new WiredFurniVariableEventMessageComposer { WiredData = snapshot },
        };

    private static byte[] Sent(IComposer composer)
    {
        var packet = PacketHarness.Encode(composer);

        return packet.PopBytes(packet.Remaining);
    }

    private static byte[] Ints(int[] values) =>
        [
            .. values.SelectMany(v =>
                new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v }
            ),
        ];

    private static void AddBox(WiredRoom room, string classname)
    {
        var type = typeof(FurnitureWiredLogic)
            .Assembly.GetTypes()
            .SingleOrDefault(type =>
                type.GetCustomAttribute<RoomObjectLogicAttribute>(false)?.Key == classname
            );

        type.Should().NotBeNull($"{classname} is a wired box on Habbo");

        typeof(WiredRoom)
            .GetMethod(nameof(WiredRoom.AddBox))!
            .MakeGenericMethod(type!)
            .Invoke(room, [BOX, 0, 0, classname]);
    }

    private static Dictionary<string, JsonElement> Load()
    {
        using var stream =
            typeof(WiredOfficialEditorContractTests).Assembly.GetManifestResourceStream(
                "Rooms.Fixtures.official-wired-editor-opens.json"
            )!;
        using var json = JsonDocument.Parse(stream);

        return json
            .RootElement.EnumerateArray()
            .ToDictionary(x => x.GetProperty("classname").GetString()!, x => x.Clone());
    }
}
