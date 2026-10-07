using System.Collections.Immutable;
using Microsoft.Extensions.Logging;
using Turbo.Commands;
using Turbo.Primitives.Action;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;

namespace Turbo.Tests.Commands;

/// <summary>
/// A live room with real command matching and a real event pipeline, and fake players who say
/// things. What a player would see is read back from the calls the room made: whispers go to one
/// presence, a room-wide line goes to the room stream.
/// </summary>
public sealed class CommandRoomFixture
{
    private readonly Dictionary<int, IRoomPlayer> _players = [];
    private readonly Dictionary<int, ResolvedPermissionsSnapshot> _snapshots = [];
    public LiveRoomHarness Harness { get; }
    public RoomConfig Config { get; }

    /// <summary>The hotel's own texts by key, which win over a default.</summary>
    public Dictionary<string, string> HotelTexts { get; } = [];
    public TestEventBus Events { get; } = new();
    public CommandRegistryProvider Commands { get; } =
        new(new CapturingLogger<ICommandRegistryProvider>());

    public CommandRoomFixture(RoomConfig? config = null)
    {
        Config = config ?? new RoomConfig();
        Harness = new LiveRoomHarness(roomConfig: Config);
        var room = Harness.Room;

        RoomHarness.SetField(room, "_eventSystem", Events.System);
        RoomHarness.SetField(room, "_commandRegistryProvider", Commands);
        RoomHarness.SetMember(
            RoomHarness.GetMember(Harness.State, "RoomSnapshot")!,
            "ChatProtection",
            ChatFloodSensitivityType.Normal
        );

        RoomHarness.SetMember(
            RoomHarness.GetMember(Harness.State, "RoomSnapshot")!,
            "ModSettings",
            ModSettingsSnapshot.OwnerOnly
        );

        var permissions = new PermissionRegistry([new CorePermissionNodeSource()]);

        Harness.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(Turbo.Primitives.Players.Providers.IPermissionRegistryProvider)
                ? permissions
                : Fakes.NotHandled;
        HotelTextFakes.Use(Harness.Fakes, key => HotelTexts.GetValueOrDefault(key));
        Harness.Fakes.Handlers["get_PlayerId"] = ForPlayer(id => (PlayerId)id);
        Harness.Fakes.Handlers["get_ObjectId"] = ForPlayer(id => (RoomObjectId)id);
        Harness.Fakes.Handlers["get_Name"] = ForPlayer(id => $"player{id}");
        Harness.Fakes.Handlers["get_Permissions"] = ForPlayer(id => _snapshots[id]);
    }

    /// <summary>
    /// Puts a player in the room who holds <paramref name="nodes"/> (and may always speak).
    /// </summary>
    public IRoomPlayer AddPlayer(int id, params string[] nodes)
    {
        var player = Harness.Fakes.Create<IRoomPlayer>(id);
        var snapshot = new ResolvedPermissionsSnapshot
        {
            Granted = [PermissionNodes.Chat.SPEAK, .. nodes],
            Meta = ImmutableDictionary<string, string>.Empty,
            UnregisteredNodes = [],
            UnregisteredMetaKeys = [],
        };

        _snapshots[id] = snapshot;
        _players[id] = player;

        var state = Harness.State;

        ((IDictionary<PlayerId, RoomObjectId>)RoomHarness.GetMember(state, "AvatarsByPlayerId")!)[
            id
        ] = id;
        (
            (IDictionary<RoomObjectId, IRoomAvatar>)
                RoomHarness.GetMember(state, "AvatarsByObjectId")!
        )[id] = player;

        return player;
    }

    /// <summary>How many lines a player may send in one flood window.</summary>
    public int FloodLimit =>
        (
            (Turbo.Rooms.Configuration.RoomConfig)RoomHarness.GetField(Harness.Room, "_roomConfig")!
        ).ChatFloodMaxMessagesNormalSensitivity;

    public bool IsInRoom(int id) =>
        (
            (IDictionary<PlayerId, RoomObjectId>)
                RoomHarness.GetMember(Harness.State, "AvatarsByPlayerId")!
        ).ContainsKey(id);

    public DateTime? MutedUntil(int id) =>
        (
            (IDictionary<PlayerId, DateTime>)
                RoomHarness.GetMember(Harness.State, "MutedUntilByPlayerId")!
        ).TryGetValue(id, out var until)
            ? until
            : null;

    public void MuteForAWhile(int id) =>
        (
            (IDictionary<PlayerId, DateTime>)
                RoomHarness.GetMember(Harness.State, "MutedUntilByPlayerId")!
        )[id] = DateTime.UtcNow.AddMinutes(10);

    public void GiveRights(int id) =>
        ((ISet<PlayerId>)RoomHarness.GetMember(Harness.State, "PlayerIdsWithRights")!).Add(id);

    public void FilterWord(string word) =>
        ((ISet<string>)RoomHarness.GetMember(Harness.State, "FilterWords")!).Add(word);

    public Task<bool> SayAsync(
        int id,
        string text,
        RoomChatType type = RoomChatType.Chat,
        string? recipient = null
    ) =>
        Harness.Room.ChatSystem.SendChatFromPlayerAsync(
            ActionContext.CreateForPlayer(id, Harness.Room.RoomId),
            type,
            text,
            ChatStyles.DEFAULT_STYLE_ID,
            -1,
            recipient,
            CancellationToken.None
        );

    /// <summary>What was said to the whole room, in order.</summary>
    public IEnumerable<string> SaidToRoom() =>
        Harness
            .Fakes.Log.Of("OnNextAsync")
            .SelectMany(c => ((RoomOutboundSnapshot)c.Args[0]!).Composers)
            .OfType<ChatMessageComposer>()
            .Select(c => c.Text);

    /// <summary>What was whispered to one player by the room.</summary>
    public IEnumerable<string> WhispersTo(int id) =>
        Harness
            .Fakes.Log.Of("SendComposerAsync")
            .Where(c => KeyIs(c.Key, id))
            .Select(c => c.Args[0])
            .OfType<WhisperMessageComposer>()
            .Select(c => c.Text);

    /// <summary>The scrollable notices sent to one player, each as its list of lines.</summary>
    public IEnumerable<IReadOnlyList<string>> NoticesTo(int id) =>
        Harness
            .Fakes.Log.Of("SendComposerAsync")
            .Where(c => KeyIs(c.Key, id))
            .Select(c => c.Args[0])
            .OfType<MOTDNotificationEventMessageComposer>()
            .Select(c => (IReadOnlyList<string>)c.Messages);

    public IEnumerable<FloodControlMessageComposer> FloodNoticesTo(int id) =>
        Harness
            .Fakes.Log.Of("SendComposerAsync")
            .Where(c => KeyIs(c.Key, id))
            .Select(c => c.Args[0])
            .OfType<FloodControlMessageComposer>();

    private static bool KeyIs(object? key, int id) =>
        key switch
        {
            PlayerId player => player.Value == id,
            int number => number == id,
            long number => number == id,
            _ => false,
        };

    private static Func<FakeCall, object?> ForPlayer(Func<int, object?> value) =>
        call =>
            call.Interface == typeof(IRoomPlayer) && call.Key is int id
                ? value(id)
                : Fakes.NotHandled;
}
