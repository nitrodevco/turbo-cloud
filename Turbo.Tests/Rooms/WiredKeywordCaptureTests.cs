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

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

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
        await Wired.OnRoomEventAsync(
            new PlayerChatEvent
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
            },
            Ct
        );
        await TickAsync(4);
    }

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

        _room.AddBox<WiredTriggerHabboSaysKeyword>(1, 0, 0, "wf_trg_says_something");
        _room.AddBox<WiredAddonVariableCapturer>(2, 0, 0, "wf_xtra_text_input_variable");
        _room.AddBox<WiredActionChangeVariable>(3, 0, 0, "wf_act_change_var_val");

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
