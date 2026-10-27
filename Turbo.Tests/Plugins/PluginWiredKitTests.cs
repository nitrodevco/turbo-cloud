using System.IO;
using System.Reflection;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Turbo.Contracts.Hotel;
using Turbo.Contracts.Wired;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Plugins;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// The public wired kit (docs/plugin-wired.md): a plugin's wired boxes and furni logic, written
/// against the contracts alone, load as contributions, are saved through core's checks and run in
/// a room's stacks under core's rules. The two documented examples (docs/examples/wired) are the
/// proof: compiled with only what a plugin project references, placed, saved and fired.
/// </summary>
public sealed class PluginWiredKitTests : IDisposable
{
    private const int TRIGGER = 1;
    private const int EFFECT = 2;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-wired-kit-" + Guid.NewGuid().ToString("N")
    );
    private readonly WiredRoom _room = new(12, 12);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public void Dispose()
    {
        _db.Dispose();

        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void The_examples_build_against_the_contracts_alone()
    {
        var (emitted, diagnostics) = CompileWithContractsOnly(Examples());

        emitted.Should().NotBeNull(string.Join("; ", diagnostics));
        Assembly
            .Load(emitted!)
            .GetReferencedAssemblies()
            .Select(x => x.Name)
            .Should()
            .NotContain(["Turbo.Rooms", "Turbo.Main", "Turbo.Plugins"]);
    }

    [Fact]
    public async Task The_example_boxes_are_listed_as_the_plugins_contributions()
    {
        PluginBuilder.Write(_folder, "wiredkit", Examples());

        var manager = new PluginTestHost(_db, _fakes).Manager(_folder);

        await manager.LoadAllAsync(ct: Ct);

        var inspected = manager.ContributionsOf("wiredkit")!;

        inspected.State.Should().Be(PluginStateType.Loaded, inspected.Error);
        inspected
            .Items.Where(x => x.Kind == PluginContributionKinds.WIRED_BOX)
            .Select(x => (x.Name, x.Detail))
            .Should()
            .BeEquivalentTo([
                ("wf_act_whisper_room", "wired effect WhisperRoomEffect"),
                ("wf_trg_credits_earned", "wired trigger CreditsEarnedTrigger"),
            ]);

        await manager.UnloadAllAsync(Ct);
    }

    /// <summary>
    /// What the docs promise: the plugin fires "credits earned" for a player, the trigger hears it
    /// in their room and the whisper effect beside it whispers the box's text to everyone in the
    /// room, each only their own; an event the trigger's rule turns down (no credits) runs nothing.
    /// </summary>
    [Fact]
    public async Task Credits_earned_fires_the_trigger_and_everyone_in_the_room_is_whispered()
    {
        var plugin = Assembly.Load(PluginBuilder.Compile("wiredkit", Examples()));
        await RegisterAsync(plugin);
        _room.Enter(5, 1, 1, "alice", playerId: 1);
        _room.Enter(6, 2, 2, "bob", playerId: 2);
        Place(TRIGGER, "wf_trg_credits_earned");
        var effect = Place(EFFECT, "wf_act_whisper_room");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0, 210, 1],
                stringParam: "Well done",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue("the box declares the three ints its editor sends");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0, 210],
                stringParam: "too few ints",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeFalse("a save is checked against the declared params, as core's boxes are");
        await StartAsync();

        await FireAsync(plugin, amount: 0, player: 1);
        await TickAsync(2);

        _room.Harness.Fakes.SentToPlayer<ChatMessageComposer>(1).Should().BeEmpty();

        await FireAsync(plugin, amount: 50, player: 1);
        await TickAsync(2);

        foreach (var player in new[] { 1, 2 })
            _room
                .Harness.Fakes.SentToPlayer<ChatMessageComposer>(player)
                .Select(x => (x.Text, x.StyleId, x.ChatBubbleWidthOverride))
                .Should()
                .Equal(
                    [("Well done", 210, (int?)1)],
                    "each user is whispered once, only their own, in the bubble picked"
                );
        (await ((FurnitureWiredLogic)effect.Logic).RefreshEditorContextAsync(Ct))
            .Should()
            .BeTrue("its editor lists the stack's placeholders, read again each time it opens");
    }

    /// <summary>
    /// A plugin's contract-only furni logic: a use it handles sets its state and fires its own
    /// wired event in the room, with the user as the triggerer. A plugin trigger that throws on the
    /// same stack is logged and passed over, not the end of the room's wired tick.
    /// </summary>
    [Fact]
    public async Task Plugin_furni_logic_handles_a_use_and_fires_its_trigger_in_the_room()
    {
        var plugin = Assembly.Load(PluginBuilder.Compile("buttons", [BUTTON]));
        await RegisterAsync(plugin);
        var alice = _room.Enter(5, 1, 1, "alice", playerId: 1);
        var button = Place(10, "plugin_button", x: 3, y: 3);
        Place(TRIGGER, "wf_trg_button_pressed");
        var broken = Place(3, "wf_trg_broken", x: 8, y: 8);
        _room.AddBox<WiredActionFreezeUser>(EFFECT, 6, 6, "wf_act_freeze");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER)).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0, 0],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await StartAsync();

        (
            await _room
                .Harness.Module<RoomActionModule>()
                .UseItemByIdAsync(_room.ContextFor(5), button.ObjectId, Ct, param: 0)
        )
            .Should()
            .BeTrue();
        await TickAsync(2);

        button.Logic.GetState().Should().Be(7, "the plugin handled the use");
        alice.IsFrozen.Should().BeTrue("the button's event fired the stack, alice triggering");
        await ((Turbo.Primitives.Rooms.Wired.IWiredTrigger)broken.Logic).LoadWiredAsync(Ct);
        (
            await ((Turbo.Primitives.Rooms.Wired.IWiredTrigger)broken.Logic).MatchesEventAsync(
                Event(plugin, "Buttons.ButtonPressed"),
                Ct
            )
        )
            .Should()
            .BeFalse("a trigger that throws is passed over");
    }

    /// <summary>
    /// The hotel hands the room the event as its key and JSON, the triggerer as the player who
    /// caused it: the room's triggers match the key and read the JSON back as the plugin's type.
    /// </summary>
    [Fact]
    public async Task The_hotel_hands_the_room_the_event_its_key_and_its_triggerer()
    {
        var fakes = new Fakes();
        fakes.Handlers["GetRoomPopulationAsync"] = _ => Task.FromResult(1);
        var hotel = new Turbo.Main.Hotel.HostHotelWired(
            fakes.Create<Orleans.IGrainFactory>(),
            fakes.Create<IHotelPlayers>()
        );

        (await hotel.FireAsync(new(4), new Pinged(2), new PlayerId(9), Ct)).Should().BeTrue();

        var published = fakes
            .Log.Of("PublishRoomEventAsync")
            .Single()
            .Args[0]
            .Should()
            .BeOfType<PluginWiredEvent>()
            .Subject;
        published.EventKey.Should().Be(WiredEvents.KeyOf(typeof(Pinged)));
        published.Payload.Should().Be("""{"Amount":2}""");
        published.CausedBy.PlayerId.Should().Be(new PlayerId(9));
    }

    private sealed record Pinged(int Amount) : IWiredEvent;

    private async Task RegisterAsync(Assembly plugin) =>
        await new PluginLogicFeatureProcessor(_room.Harness.LogicProvider).ProcessAsync(
            plugin,
            _room.Harness.Services,
            Ct
        );

    private Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItem Place(
        int id,
        string key,
        int x = 6,
        int y = 6
    ) =>
        _room.AddFloorItem(
            id,
            x,
            y,
            key,
            createLogic: (_, ctx) => _room.Harness.LogicProvider.CreateLogicInstance(key, ctx)
        );

    private Task FireAsync(Assembly plugin, int amount, int player) =>
        Wired.OnRoomEventAsync(
            Event(plugin, "WiredExamples.CreditsEarned", new PlayerId(player), amount),
            Ct
        );

    /// <summary>The plugin's event as the hotel API hands it to the room.</summary>
    private PluginWiredEvent Event(
        Assembly plugin,
        string typeName,
        PlayerId? triggerer = null,
        params object[] args
    )
    {
        var type = plugin.GetType(typeName)!;

        return (PluginWiredEvent)
            typeof(PluginWiredEvents)
                .GetMethod(nameof(PluginWiredEvents.FromHotel))!
                .MakeGenericMethod(type)
                .Invoke(
                    null,
                    [_room.Harness.Room.RoomId, Activator.CreateInstance(type, args), triggerer]
                )!;
    }

    private async Task StartAsync()
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(6, 6)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }

    private static IReadOnlyList<string> Examples()
    {
        var folder = Path.Combine(RepositoryRoot(), "docs", "examples", "wired");

        return [.. Directory.EnumerateFiles(folder, "*.cs").Order().Select(File.ReadAllText)];
    }

    /// <summary>
    /// Compiled with what a plugin project references (templates/turbo-plugin/server): the
    /// runtime, Turbo.Contracts and Turbo.Primitives with the Orleans abstractions they name.
    /// No room, grain or host assembly.
    /// </summary>
    private static (byte[]? Emitted, IEnumerable<string> Diagnostics) CompileWithContractsOnly(
        IReadOnlyList<string> sources
    )
    {
        var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(file =>
                string.Equals(
                    Path.GetDirectoryName(file),
                    runtime,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Concat(
                new[]
                {
                    typeof(WiredBox).Assembly,
                    typeof(PlayerId).Assembly,
                    typeof(Orleans.GenerateSerializerAttribute).Assembly,
                    typeof(Orleans.IGrainFactory).Assembly,
                }.Select(x => x.Location)
            )
            .Distinct()
            .Select(file => MetadataReference.CreateFromFile(file));
        var compilation = CSharpCompilation.Create(
            "wiredkit",
            sources.Select(source => CSharpSyntaxTree.ParseText(source)),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        using var dll = new MemoryStream();
        var result = compilation.Emit(dll);

        return (
            result.Success ? dll.ToArray() : null,
            result
                .Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
        );
    }

    private static string RepositoryRoot()
    {
        for (
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent
        )
            if (File.Exists(Path.Combine(dir.FullName, "Turbo.Cloud.sln")))
                return dir.FullName;

        throw new InvalidOperationException("Turbo.Cloud.sln was not found above the tests.");
    }

    private const string BUTTON = """
        using System.Threading;
        using System.Threading.Tasks;
        using Turbo.Contracts.Furni;
        using Turbo.Contracts.Rooms;
        using Turbo.Contracts.Wired;
        using Turbo.Primitives.Rooms.Enums.Wired;

        namespace Buttons;

        public sealed record ButtonPressed : IWiredEvent;

        [FurniLogic("plugin_button")]
        public sealed class Button : FurniLogic
        {
            public override async Task<bool> OnUseAsync(IFurniContext ctx, RoomUser? user, int param, CancellationToken ct)
            {
                await ctx.SetStateAsync(7, ct);
                await ctx.FireWiredAsync(new ButtonPressed(), user, ct);

                return true;
            }
        }

        [WiredBox("wf_trg_button_pressed")]
        public sealed class ButtonPressedTrigger : WiredTrigger<ButtonPressed>
        {
            public override WiredTriggerType Editor => WiredTriggerType.GAME_STARTS;
        }

        [WiredBox("wf_trg_broken")]
        public sealed class BrokenTrigger : WiredTrigger<ButtonPressed>
        {
            public override WiredTriggerType Editor => WiredTriggerType.GAME_STARTS;

            public override bool Matches(ButtonPressed evt, IWiredBoxConfig box) =>
                throw new System.InvalidOperationException("a plugin's bug");
        }
        """;
}
