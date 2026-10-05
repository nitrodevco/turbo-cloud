using System.Collections.Immutable;
using Microsoft.Extensions.Options;
using Turbo.Commands;
using Turbo.Players.Configuration;
using Turbo.Players.Notifications;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;

namespace Turbo.Tests.Commands;

/// <summary>Whoever ran an operator command in a test: it keeps what it was told.</summary>
public sealed class FakeExecutor : IOperatorExecutor
{
    private readonly HashSet<string> _nodes;

    public FakeExecutor(
        int? playerId,
        string name,
        int? roomId = null,
        IEnumerable<int>? roomPlayers = null,
        params string[] nodes
    )
    {
        PlayerId = playerId;
        Name = name;
        RoomId = roomId;
        RoomPlayerIds = [.. (roomPlayers ?? []).Select(x => (PlayerId)x)];
        _nodes = [.. nodes];
    }

    public PlayerId? PlayerId { get; }

    public string Name { get; }

    /// <summary>Where the command came from; unset, what the contract says by default.</summary>
    public string? SourceOverride { get; set; }

    public string Source => SourceOverride ?? (PlayerId is null ? "console" : "player");

    public RoomId? RoomId { get; }

    public IReadOnlyList<PlayerId> RoomPlayerIds { get; }

    public List<string> Replies { get; } = [];

    public List<IReadOnlyList<string>> Notices { get; } = [];

    /// <summary>Takes a node away, as a demotion would while a line waits.</summary>
    public void Revoke(string node) => _nodes.Remove(node);

    public Task<bool> HasAsync(string node, CancellationToken ct) =>
        Task.FromResult(PlayerId is null || _nodes.Contains(node));

    public Task ReplyAsync(string text, CancellationToken ct)
    {
        Replies.Add(text);

        return Task.CompletedTask;
    }

    public Task NoticeAsync(IReadOnlyList<string> lines, CancellationToken ct)
    {
        Notices.Add(lines);

        return Task.CompletedTask;
    }
}

/// <summary>
/// The real operator runner, with the real command registry and event pipeline, over a fake grain
/// factory: a player directory that knows a few names, a session gateway with a few players
/// online, and hotel texts a test can set.
/// </summary>
public sealed class OperatorFixture
{
    private readonly Dictionary<string, int> _idsByName = new(StringComparer.OrdinalIgnoreCase);

    public Fakes Fakes { get; } = new();

    public InMemoryDb Db { get; } = new();

    public TestEventBus Events { get; } = new();

    public CommandRegistryProvider Commands { get; } =
        new(new CapturingLogger<ICommandRegistryProvider>());

    public CapturingLogger<IOperatorCommandRunner> Log { get; } = new();

    /// <summary>The hotel's own texts by key, which win over a default.</summary>
    public Dictionary<string, string> HotelTexts { get; } = [];

    /// <summary>Who is online.</summary>
    public List<PlayerId> Online { get; } = [];

    public OperatorCommandRunner Runner { get; }

    public CommandConfig Config { get; }

    /// <summary>The clock confirmations expire by.</summary>
    public ManualTimeProvider Clock { get; } =
        new(new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc));

    public OperatorFixture(CommandConfig? config = null)
    {
        Config = config ?? new CommandConfig();
        Fakes.Handlers["TryGetText"] = call =>
        {
            if (call.Args[0] is not string key || !HotelTexts.TryGetValue(key, out var text))
                return false;

            call.Args[1] = text;

            return true;
        };
        Fakes.Handlers["GetPlayerIdAsync"] = call =>
            Task.FromResult<PlayerId?>(
                _idsByName.TryGetValue((string)call.Args[0]!, out var id) ? id : null
            );
        Fakes.Handlers["GetPlayerNameAsync"] = call =>
            Task.FromResult(
                _idsByName.FirstOrDefault(x => x.Value == ((PlayerId)call.Args[0]!).Value).Key
                    ?? string.Empty
            );
        Fakes.Handlers["GetPlayerNamesAsync"] = call =>
            Task.FromResult(
                ((List<PlayerId>)call.Args[0]!)
                    .Where(id => _idsByName.ContainsValue(id.Value))
                    .ToImmutableDictionary(
                        id => id,
                        id => _idsByName.First(x => x.Value == id.Value).Key
                    )
            );
        Fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)Online;
        Fakes.Handlers["TrySendComposerAsync"] = call =>
            Task.FromResult(Online.Any(id => id.Value == (long)call.Key!));

        Runner = new OperatorCommandRunner(
            Commands,
            Fakes.Create<IHotelTextProvider>(),
            Events.System,
            Fakes.Create<Orleans.IGrainFactory>(),
            Fakes.Create<ISessionGateway>(),
            new CommandBatchExecutor(
                Microsoft.Extensions.Options.Options.Create(Config),
                new CapturingLogger<ICommandBatchExecutor>()
            ),
            new PlayerNoticeService(
                Fakes.Create<Orleans.IGrainFactory>(),
                Fakes.Create<IHotelTextProvider>(),
                Options.Create(new PlayerConfig()),
                new CapturingLogger<IPlayerNoticeService>()
            ),
            Db,
            Microsoft.Extensions.Options.Options.Create(Config),
            Clock,
            Log
        );
    }

    /// <summary>A player the hotel's directory knows by name.</summary>
    public OperatorFixture WithPlayer(int id, string name, bool online = true)
    {
        _idsByName[name] = id;

        if (online)
            Online.Add(id);

        return this;
    }

    public CommandDescriptor Find(string name)
    {
        Commands.Current.TryFind(name, out var descriptor);

        return descriptor;
    }

    public Task<CommandOutcome> RunAsync(
        string name,
        FakeExecutor executor,
        string arguments,
        bool checkNode = true
    ) => Runner.RunAsync(Find(name), executor, arguments, checkNode, CancellationToken.None);

    /// <summary>The player grains the fake factory handed out, for a test to read what was called.</summary>
    public IEnumerable<FakeCall> Calls<TGrain>(string method)
        where TGrain : class => Fakes.Log.On<TGrain>().Where(x => x.Method == method);

    public IPlayerDirectoryGrain Directory => Fakes.Create<IPlayerDirectoryGrain>("directory");
}
