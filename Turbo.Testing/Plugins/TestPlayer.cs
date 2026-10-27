using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Messages.PluginMessages;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms;

namespace Turbo.Testing;

/// <summary>
/// A player of a <see cref="TestHotel"/>, signed in on a session of their own: they stand in rooms,
/// say commands and send the client's messages, and everything the hotel and the plugins send
/// them is kept (<see cref="Received"/>, <see cref="Whispers"/>). Passes as their
/// <see cref="PlayerId"/> and <see cref="FakePlayer"/>.
/// </summary>
public sealed class TestPlayer
{
    private readonly TestHotel _hotel;
    private readonly List<object> _session = [];

    internal TestPlayer(TestHotel hotel, FakePlayer player)
    {
        _hotel = hotel;
        Fake = player;
        SessionKey = new SessionKey($"test-session-{player.Id.Value}");
    }

    /// <summary>The player as the hotel API holds them: their name, club, permission nodes...</summary>
    public FakePlayer Fake { get; }

    public PlayerId Id => Fake.Id;

    public string Name => Fake.Name;

    public SessionKey SessionKey { get; }

    internal TestHotel Hotel => _hotel;

    /// <summary>The room the player stands in; null in none.</summary>
    public TestRoom? Room { get; private set; }

    /// <summary>What their last command came to; null before one.</summary>
    public CommandOutcome? LastCommand { get; internal set; }

    /// <summary>
    /// Everything the player was sent, in order: the messages and composers plugins sent them
    /// through the hotel API, and what their session was answered (a plugin message decoded back
    /// to its record).
    /// </summary>
    public IReadOnlyList<object> Received =>
        [.. _hotel.Fake.Players.SentTo(Id).Select(Decode).Concat(_session)];

    /// <summary>The whispers the player was sent (a command's reply among them), in order.</summary>
    public IReadOnlyList<string> Whispers => _hotel.Fake.Rooms.WhispersTo(Id);

    /// <summary>The client notifications the player was shown, in order.</summary>
    public IReadOnlyList<FakeNotification> Notifications =>
        [.. _hotel.Notify.Notifications.Where(n => n.Player == Id)];

    public static implicit operator PlayerId(TestPlayer player) => player.Id;

    public static implicit operator FakePlayer(TestPlayer player) => player.Fake;

    /// <summary>Puts the player in room <paramref name="roomId"/> (added on first use), out of any other.</summary>
    public TestPlayer InRoom(int roomId)
    {
        var room = _hotel.Room(roomId);

        _hotel.Fake.Rooms.Enter(room.Fake, Fake);
        Room = room;

        return this;
    }

    /// <summary>Takes the player out of their room.</summary>
    public TestPlayer LeavesRoom()
    {
        _hotel.Fake.Rooms.Leave(Fake);
        Room = null;

        return this;
    }

    /// <summary>Grants the player permission nodes (<c>*</c>: all of them).</summary>
    public TestPlayer WithPermissions(params string[] nodes)
    {
        foreach (var node in nodes)
            Fake.Permissions.Add(node);

        return this;
    }

    /// <summary>A player who holds every node, as hotel staff with all of them do.</summary>
    public TestPlayer AsStaff() => WithPermissions("*");

    /// <summary>
    /// The player says <paramref name="text"/> in their room. A <c>:command</c> is run as the room
    /// runs one: a node it needs, the room level it needs, its arguments, then the command, its
    /// result whispered from the hotel's texts (the plugin's <c>gamedata/external_texts.txt</c>),
    /// else core's, else the command's own defaults. Other lines reach no plugin here.
    /// </summary>
    public async Task<TestPlayer> SaysAsync(string text, CancellationToken ct = default)
    {
        var room =
            Room
            ?? throw new HotelAssertionException(
                $"{Name} says \"{text}\" in no room: put them in one first (InRoom(id))."
            );

        await TestCommands.RunAsync(_hotel, this, room, text, ct).ConfigureAwait(false);

        return this;
    }

    /// <summary>
    /// The player's client sends <paramref name="message"/>, a plugin's named message: written and
    /// read back by its schema as on the wire, then handled by the hotel's packet pipeline (its
    /// sign-in and permission gates, then every handler of it).
    /// </summary>
    public Task<TestPlayer> SendsAsync(IPluginMessage message, CancellationToken ct = default)
    {
        var schema = PluginMessageSchema.For(message.GetType());
        var body = PluginMessageComposer.For(message).Body;
        var parsed = new PluginMessageParser(schema).Parse(new ClientPacket(0, body.AsMemory()));

        return SendsAsync(parsed, ct);
    }

    /// <summary>The player's client sends a core packet, handled by the hotel's packet pipeline (the plugins' handlers of it).</summary>
    public async Task<TestPlayer> SendsAsync(IMessageEvent message, CancellationToken ct = default)
    {
        await _hotel.Packets.PublishAsync(message, Session(), ct).ConfigureAwait(false);

        return this;
    }

    internal ISessionContext Session()
    {
        var session = _hotel.Sessions.Of<ISessionContext>(Id.Value);

        _hotel.Sessions.Answer<ISessionContext>("get_SessionKey", call => KeyOf(call.Key));
        _hotel.Sessions.Answer<ISessionContext>(
            "get_ActiveRoomId",
            call => _hotel.PlayerOf(PlayerOf(call.Key)).Room?.Id ?? RoomId.Invalid
        );
        _hotel.Sessions.Answer<ISessionContext>(
            nameof(ISessionContext.SendComposerAsync),
            call =>
            {
                _hotel.PlayerOf(PlayerOf(call.Key))._session.Add(Decode(call.Args[0]!));

                return Task.CompletedTask;
            }
        );

        return session;
    }

    private static SessionKey KeyOf(object? key) => new($"test-session-{key}");

    /// <summary>The player a stand-in's key names (a grain's <c>long</c>, a session's <c>int</c>).</summary>
    internal static PlayerId PlayerOf(object? key) =>
        key switch
        {
            int n => new PlayerId(n),
            long n => new PlayerId((int)n),
            PlayerId p => p,
            _ => PlayerId.Invalid,
        };

    /// <summary>A plugin message composer read back into its record; anything else as it was.</summary>
    private object Decode(object sent)
    {
        if (sent is not PluginMessageComposer composer)
            return sent;

        foreach (var plugin in _hotel.Plugins.Values)
        foreach (var type in MessageTypes(plugin.Assembly))
        {
            var schema = PluginMessageSchema.For(type);

            if (schema.Name == composer.Name)
                return schema.Read(new ClientPacket(0, composer.Body.AsMemory()));
        }

        return sent;
    }

    private static IEnumerable<Type> MessageTypes(Assembly assembly) =>
        Turbo.Runtime.AssemblyProcessing.AssemblyExplorer.FindAssignees(
            assembly,
            typeof(IPluginMessage)
        );

    /// <summary>Whether the player holds <paramref name="node"/> (<c>*</c> holds them all).</summary>
    internal static bool Holds(FakePlayer player, string node) =>
        player.Permissions.Contains("*") || player.Permissions.Contains(node);

    public override string ToString() => Fake.ToString();
}
