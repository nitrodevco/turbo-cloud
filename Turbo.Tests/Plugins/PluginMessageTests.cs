using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans.Serialization;
using Orleans.Streams;
using Turbo.Messages;
using Turbo.Messages.PluginMessages;
using Turbo.Messages.Registry;
using Turbo.Networking.Extensions;
using Turbo.Networking.Package;
using Turbo.Networking.Revisions;
using Turbo.PacketHandlers.Turbo;
using Turbo.Plugins;
using Turbo.Plugins.Configuration;
using Turbo.Primitives.Availability;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Messages.Incoming.Turbo;
using Turbo.Primitives.Messages.Outgoing.Turbo;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Networking.Capabilities;
using Turbo.Primitives.Networking.Extensions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Plugins.Messages;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Revisions.Revision20260909;
using Turbo.Rooms.Grains;
using Turbo.Runtime.AssemblyProcessing;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Plugins;

/// <summary>
/// Named plugin messages (<c>docs/plugin-messages.md</c>): a plugin declares a record and a name,
/// the host gives it a header no packet uses, writes and reads it from its fields, tells clients
/// that accepted <c>client.messages</c> the name, header and schema, routes what a client sends to
/// the plugin's handler through the packet pipeline, and takes all of it away when the plugin goes.
/// </summary>
public sealed class PluginMessageTests : IDisposable
{
    private const int PLAYER = 103;
    private const string LEAK = "did not unload";

    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "turbo-plugin-messages-" + Guid.NewGuid().ToString("N")
    );
    private readonly Fakes _fakes = new();
    private readonly ExtensionPacketRegistry _extensions = new();
    private readonly PluginMessageDirectory _directory;
    private readonly CapturingLogger<PluginManager> _log = new();

    public PluginMessageTests()
    {
        Directory.CreateDirectory(_folder);

        _directory = new PluginMessageDirectory(_extensions);
        _fakes.Handlers["GetPlayerId"] = _ => new PlayerId(PLAYER);
        _fakes.Handlers["GetOnlinePlayerIds"] = _ =>
            (IReadOnlyCollection<PlayerId>)new List<PlayerId> { PLAYER };
        _fakes.Handlers["get_CryptoOut"] = _ => null;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    // ---- The codec ----

    [Fact]
    public void Every_supported_field_type_round_trips_through_the_wire_including_records_and_arrays()
    {
        var sent = new Everything(
            -7,
            1L << 40,
            true,
            "héllo",
            Math.PI,
            Mood.Happy,
            new Inner("in", [1, 2, 3]),
            [new Inner("a", []), new Inner("b", [9])],
            ["x", "y"],
            [
                [1L, 2L],
                [3L],
            ]
        );

        var read = RoundTrip(sent);

        read.Should().BeEquivalentTo(sent);
        read.Should().BeOfType<Everything>().Which.Records.Should().HaveCount(2);
    }

    [Fact]
    public void A_null_record_or_array_is_written_as_its_defaults()
    {
        var read = (Everything)RoundTrip(
            new Everything(0, 0, false, null!, 0, Mood.Sad, null!, default, null!, null!)
        );

        read.Text.Should().BeEmpty();
        read.Nested.Should().Be(new Inner("", []));
        read.Records.Should().BeEmpty();
        read.Words.Should().BeEmpty();
        read.Grid.Should().BeEmpty();
    }

    [Fact]
    public void The_schema_clients_are_sent_names_every_field_in_order_in_camel_case()
    {
        PluginMessageSchema
            .For(typeof(Everything))
            .Json.Should()
            .Be(
                """[["number","int"],["big","long"],["flag","bool"],["text","string"],["real","double"],["mood","int"],"""
                    + """["nested",["object",[["label","string"],["values",["array","int"]]]]],"""
                    + """["records",["array",["object",[["label","string"],["values",["array","int"]]]]]],"""
                    + """["words",["array","string"]],["grid",["array",["array","long"]]]]"""
            );
    }

    [Fact]
    public void The_fields_are_written_in_habbos_wire_encoding()
    {
        var body = PluginMessageComposer.For(new Small(5, "ab", true, [7L])).Body.ToArray();

        body.Should()
            .Equal([0, 0, 0, 5, 0, 2, (byte)'a', (byte)'b', 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 7]);
    }

    [Fact]
    public void A_string_keyed_dictionary_round_trips_as_a_map_of_its_values_records_and_lists_included()
    {
        // A plugin had to send its key/value pairs as a list of records before (fwgaps 10-25).
        var sent = new WithDictionary(
            new Dictionary<string, int> { ["mining"] = 2, ["fishing"] = 0 },
            new Dictionary<string, Inner> { ["a"] = new Inner("x", [1, 2]) },
            ImmutableDictionary<string, ImmutableArray<string>>.Empty.Add("tags", ["red", "blue"])
        );

        var read = (WithDictionary)RoundTrip(sent);

        read.Counts.Should().Equal(sent.Counts);
        read.Records.Should().ContainKey("a").WhoseValue.Should().Be(new Inner("x", [1, 2]));
        read.Lists.Should().ContainKey("tags").WhoseValue.Should().Equal("red", "blue");
        PluginMessageSchema
            .For(typeof(WithDictionary))
            .Json.Should()
            .Be(
                """[["counts",["map","int"]],["records",["map",["object",[["label","string"],["values",["array","int"]]]]]],["lists",["map",["array","string"]]]]"""
            );
    }

    [Fact]
    public void A_dictionary_is_written_as_a_count_then_each_key_and_value_and_null_as_none()
    {
        var body = PluginMessageComposer
            .For(new WithDictionary(new() { ["a"] = 5 }, null!, null!))
            .Body.ToArray();

        body.Should().Equal([0, 0, 0, 1, 0, 1, (byte)'a', 0, 0, 0, 5, 0, 0, 0, 0, 0, 0, 0, 0]);
    }

    [Fact]
    public void A_field_the_codec_cannot_carry_is_refused_naming_the_field()
    {
        var act = () => PluginMessageSchema.For(typeof(WithIntKeys));

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*test.intKeys.ById*keys must be strings*");
    }

    [Fact]
    public void A_record_that_contains_itself_is_refused()
    {
        var act = () => PluginMessageSchema.For(typeof(Tree));

        act.Should().Throw<ArgumentException>().WithMessage("*Children*cannot contain itself*");
    }

    [Fact]
    public void A_hostile_array_count_is_refused_not_allocated()
    {
        var schema = PluginMessageSchema.For(typeof(Small));
        var payload = PacketHarness.Payload(w => w.Int(1).String("a").Bool(true).Int(int.MaxValue));

        var act = () => schema.Read(new ClientPacket(1, payload));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void A_hostile_dictionary_count_is_refused_not_allocated()
    {
        var schema = PluginMessageSchema.For(typeof(WithDictionary));
        var payload = PacketHarness.Payload(w => w.Int(int.MaxValue).String("a"));

        var act = () => schema.Read(new ClientPacket(1, payload));

        act.Should().Throw<InvalidDataException>();
    }

    // ---- The directory ----

    [Fact]
    public void A_name_that_does_not_start_with_the_plugins_key_is_refused()
    {
        var act = () => _directory.Register("other", [typeof(Small)]);

        act.Should().Throw<ArgumentException>().WithMessage("*\"test.small\"*\"other.\"*");
        _directory.Messages.Should().BeEmpty();
    }

    [Fact]
    public void A_name_another_plugin_registered_is_refused_and_nothing_of_the_second_is_kept()
    {
        using var first = _directory.Register("test", [typeof(Small)]);

        var act = () => _directory.Register("test", [typeof(Everything), typeof(SmallAgain)]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*\"test.small\"*test*");
        _directory.Messages.Select(x => x.Name).Should().Equal("test.small");
        _directory.TryGetHeader("test.everything", out _).Should().BeFalse();
    }

    [Fact]
    public void A_type_without_the_attribute_is_refused()
    {
        var act = () => _directory.Register("test", [typeof(Unnamed)]);

        act.Should().Throw<ArgumentException>().WithMessage("*Unnamed*[PluginMessage*");
    }

    [Fact]
    public void Headers_come_from_the_top_of_the_plugin_space_past_every_header_a_packet_uses()
    {
        using var parser = _extensions.RegisterParser(32767, new NothingParser());
        using var serializer = _extensions.RegisterSerializer(
            typeof(HandNumbered),
            new HandNumberedSerializer(32766)
        );

        using var registration = _directory.Register("test", [typeof(Small), typeof(Everything)]);

        _directory
            .Messages.Select(x => (x.Name, x.Header))
            .Should()
            .Equal(("test.everything", 32764), ("test.small", 32765));
        _extensions.HasHeader(32765).Should().BeTrue("the header reads the message now");
    }

    [Fact]
    public void A_hand_numbered_plugin_cannot_take_a_header_a_message_reads()
    {
        using var registration = _directory.Register("test", [typeof(Small)]);
        _directory.TryGetHeader("test.small", out var header).Should().BeTrue();

        var act = () => _extensions.RegisterParser(header, new NothingParser());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void A_name_gets_its_header_back_when_its_plugin_registers_again()
    {
        var first = _directory.Register("test", [typeof(Small)]);
        _directory.TryGetHeader("test.small", out var before);
        first.Dispose();

        using var other = _directory.Register("test", [typeof(Everything)]);
        using var again = _directory.Register("test", [typeof(Small)]);

        _directory.TryGetHeader("test.small", out var after).Should().BeTrue();
        after.Should().Be(before);
        _directory.TryGetHeader("test.everything", out var otherHeader);
        otherHeader.Should().NotBe(before, "a header another name had is given last");
    }

    // ---- A plugin, end to end ----

    [Fact]
    public async Task A_plugin_using_only_the_contracts_has_its_messages_listed_and_its_handler_reached_from_client_bytes()
    {
        await using var hotel = await LoadSkillsAsync();

        _directory
            .Messages.Select(x => x.Name)
            .Should()
            .Equal("skills.levelUp", "skills.secret", "skills.state");
        var levelUp = _directory.Messages.Single(x => x.Name == "skills.levelUp");
        levelUp.Schema.Should().Be("""[["skill","string"],["path",["array","int"]]]""");

        var session = await hotel.SendAsync(
            levelUp.Header,
            PacketHarness.Payload(w => w.String("mining").Int(3).Int(1).Int(2).Int(3))
        );

        var reply = session.Should().ContainSingle().Which;
        var state = Decode(reply);

        state.Name.Should().Be("skills.state");
        state.Header.Should().Be(_directory.Messages.Single(x => x.Name == "skills.state").Header);
        state.Read.Should().Be("total=3 skills=[mining:6:1099511627776] max=True");
    }

    [Fact]
    public async Task A_handlers_RequiresPermission_is_honoured()
    {
        await using var hotel = await LoadSkillsAsync();
        var secret = _directory.Messages.Single(x => x.Name == "skills.secret").Header;

        (await hotel.SendAsync(secret, PacketHarness.Payload(w => w.Int(1))))
            .Should()
            .BeEmpty("the player does not hold skills.secret");

        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);

        (await hotel.SendAsync(secret, PacketHarness.Payload(w => w.Int(1))))
            .Should()
            .ContainSingle();
    }

    [Fact]
    public async Task A_session_with_no_signed_in_player_never_reaches_the_handler()
    {
        await using var hotel = await LoadSkillsAsync();
        var levelUp = _directory.Messages.Single(x => x.Name == "skills.levelUp").Header;
        _fakes.Handlers["GetPlayerId"] = _ => new PlayerId(0);

        (await hotel.SendAsync(levelUp, PacketHarness.Payload(w => w.String("a").Int(0))))
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task Unloading_the_plugin_takes_its_messages_headers_and_handlers_away_and_tells_clients()
    {
        var broadcast = new PluginMessageBroadcast(
            _directory,
            _fakes.Create<Orleans.IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            NullLogger<PluginMessageBroadcast>.Instance
        );
        await broadcast.StartAsync(Ct);

        var hotel = await LoadSkillsAsync();
        var levelUp = _directory.Messages.Single(x => x.Name == "skills.levelUp").Header;

        await hotel.DisposeAsync();

        _directory.Messages.Should().BeEmpty();
        _directory.TryGetHeader("skills.levelUp", out _).Should().BeFalse();
        _extensions.TryGetParser(levelUp, out _).Should().BeFalse();
        _extensions.HasHeader(levelUp).Should().BeFalse();

        var sent = _fakes.SentToPlayer<TurboPluginMessagesMessage>(PLAYER).ToList();
        sent.Should().HaveCount(2);
        sent[0].Messages.Should().HaveCount(3);
        sent[1].Messages.Should().BeEmpty("the whole set, without the plugin's");
        LeakDump.IfHeld(_log.Entries, Path.GetFileName(_folder));
        _log.AtLeast(LogLevel.Warning).Where(x => x.Message.Contains(LEAK)).Should().BeEmpty();

        await broadcast.StopAsync(Ct);
    }

    [Fact]
    public async Task A_plugin_declaring_a_message_outside_its_key_fails_to_load_and_keeps_nothing()
    {
        PluginBuilder.Write(
            _folder,
            "skills",
            SKILLS.Replace("\"skills.secret\"", "\"other.secret\"")
        );
        var manager = Manager(
            new MessageRegistry(HostServices(), NullLogger<MessageRegistry>.Instance)
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Failed);
        status.Error.Should().Contain("\"other.secret\"").And.Contain("\"skills.\"");
        _directory.Messages.Should().BeEmpty();
        _extensions.HasHeader(32767).Should().BeFalse("no header was kept for the refused load");

        await manager.UnloadAllAsync(Ct);

        LeakDump.IfHeld(_log.Entries, Path.GetFileName(_folder));

        _log.AtLeast(LogLevel.Warning).Where(x => x.Message.Contains(LEAK)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_plugin_handling_a_message_that_carries_a_packet_fails_to_load_and_keeps_nothing()
    {
        PluginBuilder.Write(
            _folder,
            "skills",
            SKILLS.Replace(
                "public sealed record Secret(int Value) : IPluginMessage;",
                "public sealed record Secret(int Value, Turbo.Primitives.Networking.IComposer? Carried = null) : IPluginMessage;"
            )
        );
        var manager = Manager(
            new MessageRegistry(HostServices(), NullLogger<MessageRegistry>.Instance)
        );

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Failed);
        status.Error.Should().Contain("skills.secret").And.Contain("carries a packet");
        _directory.Messages.Should().BeEmpty();

        await manager.UnloadAllAsync(Ct);
    }

    // ---- Sending ----

    [Fact]
    public async Task Sending_to_a_player_goes_through_the_presence_as_a_written_message()
    {
        await _fakes
            .Create<Orleans.IGrainFactory>()
            .SendPluginMessageToPlayersAsync([PLAYER, 104], new Small(1, "a", false, []), Ct);

        var expected = PluginMessageComposer.For(new Small(1, "a", false, []));

        foreach (var player in new[] { PLAYER, 104 })
        {
            var sent = _fakes
                .SentToPlayer<PluginMessageComposer>(player)
                .Should()
                .ContainSingle()
                .Which;

            sent.Name.Should().Be("test.small");
            sent.Body.Should().Equal(expected.Body);
        }
    }

    [Fact]
    public async Task A_message_sent_to_a_room_crosses_its_stream_and_is_framed_with_its_header()
    {
        using var registration = _directory.Register("test", [typeof(Small)]);
        _directory.TryGetHeader("test.small", out var header);
        var room = new RoomHarness();
        room.Room._roomOutbound = room.Fakes.Create<IAsyncStream<RoomOutboundSnapshot>>("stream");

        await room.Room.SendPluginMessageToRoomAsync(new Small(4, "r", true, [2L]), Ct);

        var carried = RoundTripOrleans(room.Fakes.RoomPublishes().Single()).Composers.Single();
        var bytes = Encode(carried);

        BinaryPrimitives.ReadInt32BigEndian(bytes).Should().Be(bytes.Length - 4);
        BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(4)).Should().Be((short)header);
        PluginMessageSchema
            .For(typeof(Small))
            .Read(new ClientPacket(header, bytes.AsMemory(6)))
            .Should()
            .BeEquivalentTo(new Small(4, "r", true, [2L]));
    }

    [Fact]
    public void A_message_whose_plugin_has_gone_is_not_written()
    {
        Encode(PluginMessageComposer.For(new Small(1, "a", false, []))).Should().BeEmpty();
    }

    // ---- The extension ----

    [Fact]
    public void Client_messages_is_a_capability_the_server_accepts()
    {
        ClientCapabilities
            .Negotiate([new ClientCapabilitySnapshot { Name = "client.messages", Version = 3 }])
            .Should()
            .Equal(new ClientCapabilitySnapshot { Name = "client.messages", Version = 1 });
    }

    [Fact]
    public async Task A_plugin_message_reaches_only_a_session_that_accepted_client_messages() =>
        await SessionHarness.InTurns(async () =>
        {
            var hotel = new SessionHarness();
            var session = hotel.NewSession(1);
            var key = session.SessionKey;

            await hotel.Gateway.AddSessionAsync(key, session);
            await hotel.Gateway.AddSessionToPlayerAsync(key, SessionHarness.PlayerId);

            var message = PluginMessageComposer.For(new Small(1, "a", false, []));

            await hotel.PresenceGrain.TrySendComposerAsync(message, Ct);
            await hotel.WhenPresenceIdleAsync();

            hotel.Received(key).Should().BeEmpty("the client never asked for client.messages");

            await hotel.PresenceGrain.SetClientCapabilitiesAsync(
                [
                    new ClientCapabilitySnapshot
                    {
                        Name = ClientCapabilities.CLIENT_MESSAGES,
                        Version = 1,
                    },
                ],
                Ct
            );
            await hotel.PresenceGrain.TrySendComposerAsync(message, Ct);
            await hotel.WhenPresenceIdleAsync();

            hotel.Received(key).Should().Equal(message);
        });

    [Fact]
    public async Task A_client_that_accepts_client_messages_is_sent_the_messages_after_the_answer()
    {
        using var registration = _directory.Register("test", [typeof(Small)]);
        var handler = new TurboClientCapabilitiesMessageHandler(
            _fakes.Create<Orleans.IGrainFactory>(),
            _extensions,
            _fakes.Create<ICommandTreeService>(),
            _fakes.Create<IHotelCountdownNotice>(),
            _fakes.Create<IClientModuleDirectory>(),
            _directory
        );

        await handler.HandleAsync(
            new TurboClientCapabilitiesMessage
            {
                Capabilities =
                [
                    new ClientCapabilitySnapshot { Name = "client.messages", Version = 1 },
                ],
            },
            ContextFor(_fakes.Create<ISessionContext>("session")),
            Ct
        );

        var sent = _fakes.Sent(SendRoute.Player).ToList();
        sent[0].Should().BeOfType<TurboServerCapabilitiesMessage>();
        sent[1]
            .Should()
            .BeOfType<TurboPluginMessagesMessage>()
            .Which.Messages.Should()
            .Equal(_directory.Messages);
    }

    [Fact]
    public void The_messages_packet_is_written_as_the_client_reads_it()
    {
        using var registration = _directory.Register("test", [typeof(Small)]);
        var packet = PacketHarness.Encode(
            new TurboPluginMessagesMessage { Messages = _directory.Messages }
        );

        packet.Header.Should().Be(30010);
        packet.PopInt().Should().Be(1);
        packet.PopString().Should().Be("test.small");
        packet.PopInt().Should().Be(32767);
        packet
            .PopString()
            .Should()
            .Be("""[["id","int"],["name","string"],["on","bool"],["ids",["array","long"]]]""");
        packet.End.Should().BeTrue();
    }

    // ---- Helpers ----

    private static IPluginMessage RoundTrip(IPluginMessage message)
    {
        var composer = PluginMessageComposer.For(message);

        return PluginMessageSchema
            .For(message.GetType())
            .Read(new ClientPacket(1, composer.Body.ToArray()));
    }

    private static RoomOutboundSnapshot RoundTripOrleans(RoomOutboundSnapshot snapshot)
    {
        var services = new ServiceCollection();
        services.AddSerializer();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<Serializer>();

        return serializer.Deserialize<RoomOutboundSnapshot>(serializer.SerializeToArray(snapshot));
    }

    private PackageEncoder Encoder()
    {
        var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

        revisions.RegisterRevision(new Revision20260909());
        _fakes.Handlers["get_RevisionId"] = _ => revisions.DefaultRevisionId;

        return new PackageEncoder(
            revisions,
            _extensions,
            new ComposerPayloadCache(),
            NullLogger<PackageEncoder>.Instance,
            _directory
        );
    }

    /// <summary>The bytes a session is written for the composer.</summary>
    private byte[] Encode(IComposer composer)
    {
        var writer = new ArrayBufferWriter<byte>();

        Encoder().Encode(writer, _fakes.Create<ISessionContext>("session"), composer);

        return writer.WrittenSpan.ToArray();
    }

    private (string Name, int Header, string Read) Decode(IComposer composer)
    {
        var bytes = Encode(composer);
        var header = BinaryPrimitives.ReadInt16BigEndian(bytes.AsSpan(4));
        var name = _directory.Messages.Single(x => x.Header == header).Name;
        var packet = new ClientPacket(header, bytes.AsMemory(6));

        return (name, header, ReadByName(composer, packet));
    }

    // The plugin's types live in its own load context: read the reply with the schema of the
    // composer's message, found by the name the directory gave its header.
    private string ReadByName(IComposer composer, ClientPacket packet)
    {
        var name = ((PluginMessageComposer)composer).Name;
        var type = _loadedTypes[name];

        return PluginMessageSchema.For(type).Read(packet).ToString()!;
    }

    private readonly Dictionary<string, Type> _loadedTypes = new(StringComparer.Ordinal);

    private ServiceProvider HostServices() =>
        new ServiceCollection()
            .AddSingleton(_fakes.Create<ISessionGateway>())
            .AddSingleton(_fakes.Create<Orleans.IGrainFactory>())
            .AddSingleton<IExtensionPacketRegistry>(_extensions)
            .AddSingleton<IPluginMessageDirectory>(_directory)
            .BuildServiceProvider();

    private PluginManager Manager(MessageRegistry registry) =>
        new(
            HostServices(),
            new AssemblyProcessor([
                new PluginMessageFeatureProcessor(
                    registry,
                    _directory,
                    NullLoggerFactory.Instance,
                    HostServices()
                ),
            ]),
            // A context still held after 50 collections is a leak, however slow the machine.
            Options.Create(
                new PluginConfig { PluginFolderPath = _folder, UnloadMinCollections = 50 }
            ),
            _log
        );

    private async Task<LoadedHotel> LoadSkillsAsync()
    {
        PluginBuilder.Write(_folder, "skills", SKILLS);

        var registry = new MessageRegistry(HostServices(), NullLogger<MessageRegistry>.Instance);
        var manager = Manager(registry);

        await manager.LoadAllAsync(ct: Ct);

        var status = manager.Snapshot().Single();
        status.State.Should().Be(PluginStateType.Loaded, status.Error);

        // The schemas of the loaded plugin's own types, to read its replies with.
        foreach (var entry in _directory.Messages)
        {
            _extensions.TryGetParser(entry.Header, out var parser).Should().BeTrue();
            _loadedTypes[entry.Name] = parser
                .GetType()
                .GetField(
                    "_schema",
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic
                )!
                .GetValue(parser)
                is PluginMessageSchema schema
                ? schema.Type
                : throw new InvalidOperationException();
        }

        return new LoadedHotel(this, manager, registry);
    }

    private static MessageContext ContextFor(ISessionContext session) =>
        new(session, new PlayerId(PLAYER), -1);

    /// <summary>The skills plugin loaded, and client bytes sent through the real packet handler.</summary>
    private sealed class LoadedHotel(
        PluginMessageTests test,
        PluginManager manager,
        MessageRegistry registry
    ) : IAsyncDisposable
    {
        private readonly PackageHandler _handler = new(
            Revisions(test._fakes),
            test._extensions,
            new MessageSystem(registry),
            NullLogger<PackageHandler>.Instance
        );

        public async Task<List<IComposer>> SendAsync(int header, byte[] payload)
        {
            var key = Guid.NewGuid();
            var session = test._fakes.Create<ISessionContext>(key);

            await _handler.Handle(session, new ClientPacket(header, payload), Ct);

            return
            [
                .. test
                    ._fakes.Log.Calls.Where(x =>
                        x.Method == "SendComposerAsync"
                        && x.Interface == typeof(ISessionContext)
                        && Equals(x.Key, key)
                    )
                    .SelectMany(x => x.Args.OfType<IComposer>()),
            ];
        }

        public async ValueTask DisposeAsync()
        {
            // The test's own hold on the plugin's types goes first: it is not the host's.
            test._loadedTypes.Clear();

            await manager.UnloadAllAsync(Ct);
        }

        private static RevisionManager Revisions(Fakes fakes)
        {
            var revisions = new RevisionManager(NullLogger<RevisionManager>.Instance);

            revisions.RegisterRevision(new Revision20260909());
            fakes.Handlers["get_RevisionId"] = _ => revisions.DefaultRevisionId;

            return revisions;
        }
    }

    private sealed class NothingParser : IParser
    {
        public IMessageEvent Parse(IClientPacket packet) => throw new NotSupportedException();
    }

    private sealed record HandNumbered : IComposer;

    private sealed class HandNumberedSerializer(int header)
        : AbstractSerializer<HandNumbered>(header)
    {
        protected override void Serialize(IServerPacket packet, HandNumbered message) { }
    }

    public enum Mood
    {
        Sad,
        Happy,
    }

    public sealed record Inner(string Label, ImmutableArray<int> Values)
    {
        public bool Equals(Inner? other) =>
            other is not null && Label == other.Label && Values.SequenceEqual(other.Values);

        public override int GetHashCode() => Label.GetHashCode();
    }

    [PluginMessage("test.everything")]
    public sealed record Everything(
        int Number,
        long Big,
        bool Flag,
        string Text,
        double Real,
        Mood Mood,
        Inner Nested,
        ImmutableArray<Inner> Records,
        string[] Words,
        IReadOnlyList<long[]> Grid
    ) : IPluginMessage;

    [PluginMessage("test.small")]
    public sealed record Small(int Id, string Name, bool On, long[] Ids) : IPluginMessage
    {
        public bool Equals(Small? other) =>
            other is not null
            && Id == other.Id
            && Name == other.Name
            && On == other.On
            && Ids.SequenceEqual(other.Ids);

        public override int GetHashCode() => Id;
    }

    [PluginMessage("test.small")]
    public sealed record SmallAgain(int Id) : IPluginMessage;

    [PluginMessage("test.dictionary")]
    public sealed record WithDictionary(
        Dictionary<string, int> Counts,
        IReadOnlyDictionary<string, Inner> Records,
        ImmutableDictionary<string, ImmutableArray<string>> Lists
    ) : IPluginMessage;

    [PluginMessage("test.intKeys")]
    public sealed record WithIntKeys(Dictionary<int, string> ById) : IPluginMessage;

    public sealed record Node(ImmutableArray<Node> Children);

    [PluginMessage("test.tree")]
    public sealed record Tree(ImmutableArray<Node> Children) : IPluginMessage;

    public sealed record Unnamed(int Value) : IPluginMessage;

    /// <summary>
    /// A plugin built against the public contracts only: Turbo.Contracts for the plugin itself,
    /// Turbo.Primitives for its messages, its handlers and the permission its secret needs.
    /// </summary>
    private const string SKILLS = """
        using System;
        using System.Collections.Immutable;
        using System.Linq;
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Turbo.Contracts.Plugins;
        using Turbo.Contracts.Plugins.Exports;
        using Turbo.Primitives.Players.Permissions;
        using Turbo.Primitives.Plugins.Messages;

        public sealed class SkillsPlugin : ITurboPlugin
        {
            public string Key => "skills";
            public string Version => "1.0.0";
            public void ConfigureServices(IServiceCollection services, PluginManifest manifest) { }
            public Task StartAsync(IServiceProvider services, CancellationToken ct) => Task.CompletedTask;
            public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
            public Task BindExportsAsync(IExportBinder binder, IServiceProvider services) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        public sealed record Skill(string Name, int Level, long Xp)
        {
            public override string ToString() => $"{Name}:{Level}:{Xp}";
        }

        [PluginMessage("skills.levelUp")]
        public sealed record LevelUp(string Skill, ImmutableArray<int> Path) : IPluginMessage;

        [PluginMessage("skills.state")]
        public sealed record SkillsState(int Total, ImmutableArray<Skill> Skills, bool Max) : IPluginMessage
        {
            public override string ToString() => $"total={Total} skills=[{string.Join(",", Skills)}] max={Max}";
        }

        [PluginMessage("skills.secret")]
        public sealed record Secret(int Value) : IPluginMessage;

        public sealed class LevelUpHandler : IPluginMessageHandler<LevelUp>
        {
            public async ValueTask HandleAsync(LevelUp message, PluginMessageContext ctx, CancellationToken ct) =>
                await ctx.ReplyAsync(
                    new SkillsState(message.Path.Length, [new Skill(message.Skill, message.Path.Sum(), 1L << 40)], true),
                    ct
                );
        }

        [RequiresPermission("skills.secret")]
        public sealed class SecretHandler : IPluginMessageHandler<Secret>
        {
            public async ValueTask HandleAsync(Secret message, PluginMessageContext ctx, CancellationToken ct) =>
                await ctx.ReplyAsync(new Secret(message.Value + 1), ct);
        }
        """;
}
