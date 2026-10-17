using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Configuration;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "User Says Keyword" with a "#(name)" token and the Variable Capturer add-on, as the Wired Faculty
/// tutorial "Advanced Automatic Shop [using 'custom contracts']" (20/03/2026) sets a price: the
/// keyword "price #(p)" fires on "price 250", the capturer puts 250 in the context variable "p", and
/// Change Variable Value copies it to the global "price".
/// </summary>
public sealed class WiredKeywordCaptureTests
{
    private const int PLAYER_INDEX = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;
    private WiredVariableRoom _price = null!;
    private WiredTriggerHabboSaysKeyword _trigger = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    // The match is time boxed by wall clock (50 ms by default): on a loaded machine a 200 character
    // pattern overran it and did not fire. These tests are about what fires, not the time box.
    public WiredKeywordCaptureTests() =>
        RoomHarness.SetField(
            _room.Harness.Room,
            "_wiredConfig",
            new WiredConfig { RegexMatchTimeoutMs = 10_000 }
        );

    [Theory]
    [InlineData("price 250", 0, 250)]
    [InlineData("PRICE 75", 0, 75)]
    [InlineData("the price 9 please", 0, 9)]
    [InlineData("the price 9 please", 1, 0)]
    [InlineData("price two", 0, 0)]
    [InlineData("hello", 0, 0)]
    public async Task The_typed_value_is_captured(string said, int matchMode, int price)
    {
        await BuildAsync(matchMode);
        await SayAsync(said);

        PriceValue().Should().Be(price);
    }

    /// <summary>
    /// The editor takes a 1000 character keyword, but Habbo fires a pattern of 191 characters and
    /// not one of 202 (official client capture).
    /// </summary>
    [Theory]
    [InlineData(191, 250)]
    [InlineData(200, 250)]
    [InlineData(202, 0)]
    public async Task A_pattern_longer_than_the_hotel_fires_on_never_fires(int length, int price)
    {
        var prefix = new string('z', length - "price #(p)".Length);

        await BuildAsync(0, prefix + "price #(p)");
        await SayAsync(prefix + "price 250");

        PriceValue().Should().Be(price);
    }

    /// <summary>
    /// Habbo lowercases the line before it captures, then looks the text up in the connector
    /// exactly first and ignoring case after (official client capture).
    /// </summary>
    [Theory]
    [InlineData("price GOLD", "1=Gold\n2=gold", 2)]
    [InlineData("price Gold", "1=Gold\n2=gold", 2)]
    [InlineData("price SILVER", "7=Silver", 7)]
    [InlineData("price bronze", "7=Silver", 0)]
    public async Task Captured_text_is_lowercased_and_matched_exactly_first(
        string said,
        string connector,
        int price
    )
    {
        await BuildAsync(0, connector: connector);
        await SayAsync(said);

        PriceValue().Should().Be(price);
    }

    /// <summary>
    /// In exact mode a marker next to another marker takes one character, trailing ones may be
    /// left empty, and a longer line never fires (official client capture, "QR:#(a)#(b)...").
    /// </summary>
    [Theory]
    [InlineData("qr:75", 7)]
    [InlineData("QR:7", 7)]
    [InlineData("qr:756", 0)]
    public async Task Adjacent_markers_in_exact_mode_take_one_character_each(string said, int price)
    {
        await BuildAsync(1, "qr:#(p)#(q)");
        await SayAsync(said);

        PriceValue().Should().Be(price);
    }

    /// <summary>
    /// A trailing exact-mode marker left empty takes the connector's line with no text ("9="),
    /// and stops the trigger without one (official client capture).
    /// </summary>
    [Theory]
    [InlineData("qr:7a", "9=\n5=a", 5)]
    [InlineData("qr:7", "9=\n5=a", 9)]
    [InlineData("qr:7", "5=a", 0)]
    public async Task An_empty_trailing_marker_takes_the_connectors_empty_line(
        string said,
        string connector,
        int price
    )
    {
        await BuildAsync(1, "qr:#(q)#(p)", connector);
        await SayAsync(said);

        PriceValue().Should().Be(price);
    }

    /// <summary>Every marker needs its own capturer; one without stops the trigger (official client capture).</summary>
    [Fact]
    public async Task A_marker_without_a_capturer_stops_the_trigger()
    {
        await BuildAsync(0, "price #(p) #(z)");
        await SayAsync("price 5 #(z)");

        PriceValue().Should().Be(0);
        (await _trigger.MatchesEventAsync(Chat("price 5 #(z)"), Ct)).Should().BeFalse();
    }

    /// <summary>
    /// In text mode, text the connector has no line for stops the trigger (official client
    /// capture); a number that does not parse is only not captured.
    /// </summary>
    [Theory]
    [InlineData("price silver", "7=Silver", true)]
    [InlineData("price bronze", "7=Silver", false)]
    [InlineData("price two", null, true)]
    public async Task Text_the_connector_lacks_stops_the_trigger(
        string said,
        string? connector,
        bool fires
    )
    {
        await BuildAsync(0, connector: connector);

        (await _trigger.MatchesEventAsync(Chat(said), Ct)).Should().Be(fires);
    }

    /// <summary>The connector's text area takes 1000 characters (VariableTextConnectorAddon).</summary>
    [Fact]
    public async Task The_text_connector_keeps_its_first_thousand_characters()
    {
        var connector = _room.AddBox<WiredAddonVariableTextConnector>(
            12,
            7,
            7,
            "wf_xtra_var_text_connector"
        );

        (
            await _room.SaveAsync<UpdateAddonMessage>(
                12,
                stringParam: "1=" + new string('a', 996) + "\n2=b"
            )
        )
            .Should()
            .BeTrue();

        connector.GetConnectors().Keys.Should().Equal(new WiredVariableValue(1));
    }

    private async Task SayAsync(string said)
    {
        await Wired.OnRoomEventAsync(Chat(said), Ct);
        await TickAsync(4);
    }

    private static PlayerChatEvent Chat(string said) =>
        new()
        {
            RoomId = 1,
            CausedBy = ActionContext.CreateForPlayer((PlayerId)(100 + PLAYER_INDEX), (RoomId)1),
            PlayerId = (PlayerId)(100 + PLAYER_INDEX),
            ObjectId = PLAYER_INDEX,
            ChatType = RoomChatType.Chat,
            StyleId = 0,
            TrackingId = 0,
            Gesture = default,
            Text = said,
        };

    private long PriceValue()
    {
        var snapshot = _price.GetVarSnapshot();

        _price
            .TryGetValue(
                new WiredVariableKey(snapshot.VariableId, WiredVariableTargetType.Global, 0),
                out var value
            )
            .Should()
            .BeTrue();

        return value.Value;
    }

    private async Task BuildAsync(
        int matchMode,
        string keyword = "price #(p)",
        string? connector = null
    )
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _price = _room.AddBox<WiredVariableRoom>(10, 4, 4, "wf_var_room");
        var p = _room.AddBox<WiredVariableContext>(11, 3, 3, "wf_var_context");

        (
            await _room.SaveAsync<UpdateVariableMessage>(
                10,
                intParams: [(int)WiredAvailabilityType.RoomActive],
                stringParam: "price"
            )
        )
            .Should()
            .BeTrue();
        (await _room.SaveAsync<UpdateVariableMessage>(11, intParams: [1], stringParam: "p"))
            .Should()
            .BeTrue();

        if (connector is not null)
        {
            _room.AddBox<WiredAddonVariableTextConnector>(12, 3, 3, "wf_xtra_var_text_connector");
            (await _room.SaveAsync<UpdateAddonMessage>(12, stringParam: connector))
                .Should()
                .BeTrue();
        }
        await _price.LoadWiredAsync(Ct);
        await p.LoadWiredAsync(Ct);
        await StartAsync(4, 4);
        await StartAsync(3, 3);

        var priceId = _price.GetVarSnapshot().VariableId.ToString();
        var pId = p.GetVarSnapshot().VariableId.ToString();

        _trigger = _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");
        _room.AddBox<WiredAddonVariableCapturer>(2, 0, 0, "wf_xtra_text_input_variable");
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");

        // A second marker's capturer, writing to a context variable of its own.
        if (keyword.Contains("#(q)"))
        {
            var q = _room.AddBox<WiredVariableContext>(13, 2, 2, "wf_var_context");

            (await _room.SaveAsync<UpdateVariableMessage>(13, intParams: [1], stringParam: "q"))
                .Should()
                .BeTrue();
            await q.LoadWiredAsync(Ct);
            await StartAsync(2, 2);

            _room.AddBox<WiredAddonVariableCapturer>(4, 0, 0, "wf_xtra_text_input_variable");
            (
                await _room.SaveAsync<UpdateAddonMessage>(
                    4,
                    intParams: [0],
                    stringParam: "q",
                    variableIds: [q.GetVarSnapshot().VariableId.ToString()]
                )
            )
                .Should()
                .BeTrue();
        }

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                intParams: [0, matchMode, 0],
                stringParam: keyword
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                2,
                intParams: [connector is null ? 0 : 1],
                stringParam: "p",
                variableIds: [pId]
            )
        )
            .Should()
            .BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                3,
                intParams:
                [
                    (int)WiredVariableTargetType.Global,
                    (int)WiredVariableOperationType.Set,
                    1,
                    0,
                    0,
                    (int)WiredVariableTargetType.Context,
                ],
                definitionSpecifics: [0],
                variableIds: [priceId, pId]
            )
        )
            .Should()
            .BeTrue();

        await StartAsync(0, 0);
    }

    private async Task StartAsync(int x, int y)
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
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
}
