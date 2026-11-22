using System.Globalization;
using FluentAssertions;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// What the editor is sent for a refused save: Habbo's <c>ErrorReport</c> (message id, error code,
/// time) for one it has no text for, which the client shows as "Sorry, received server error: 1"
/// (<c>HabboCommunicationDemo.handleErrorMessage</c>), or <c>WiredValidationError</c> with a text
/// (official client capture 2026-10-24: {errorCode 1, messageId 3051, "10/24/26, 7:40 AM"} for an
/// action, 2026-10-20: 3059 for a variable; 2026-10-24, 21 picks: 3050 for a trigger, 3052 for a
/// condition, 3055 for an add-on, 3058 for a selector, wired matrix trigger.4.E2, condition.0.E2,
/// addon.19.E2, selector.1.E2, 1/1 each).
/// </summary>
public sealed class WiredSaveRefusalPacketTests
{
    private static async Task<List<Turbo.Primitives.Packets.ClientPacket>> SaveAsync(
        WiredType kind,
        WiredSaveResult result
    )
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers["ApplyWiredUpdateAsync"] = _ => Task.FromResult(result);

        return await harness.SendAsync(
            PacketHarness.Incoming(SaveMessage(kind)),
            EmptySave(kind, objectId: 7),
            roomId: 5
        );
    }

    private static string SaveMessage(WiredType kind) =>
        kind switch
        {
            WiredType.Trigger => "UpdateTriggerMessageEvent",
            WiredType.Action => "UpdateActionMessageEvent",
            WiredType.Condition => "UpdateConditionMessageEvent",
            WiredType.Selector => "UpdateSelectorMessageEvent",
            WiredType.Addon => "UpdateAddonMessageEvent",
            WiredType.Variable => "UpdateVariableMessageEvent",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    /// <summary>
    /// A save with nothing set, laid out as the client's <c>UpdateWiredDataParser</c> reads it: the
    /// box, int params, string param, picked furni, then what the kind's parser reads (an action's
    /// delay, a condition's quantifier, a selector's filter and invert), then furni sources, user
    /// sources, variable ids and the second picked furni.
    /// </summary>
    private static byte[] EmptySave(WiredType kind, int objectId) =>
        PacketHarness.Payload(w =>
        {
            w.Int(objectId).Int(0).String(string.Empty).Int(0);

            switch (kind)
            {
                case WiredType.Action:
                case WiredType.Condition:
                    w.Int(0);
                    break;
                case WiredType.Selector:
                    w.Bool(false).Bool(false);
                    break;
                default:
                    break;
            }

            w.Int(0).Int(0).Int(0).Int(0);
        });

    [Theory]
    [InlineData(WiredType.Trigger, 3050)]
    [InlineData(WiredType.Action, 3051)]
    [InlineData(WiredType.Condition, 3052)]
    [InlineData(WiredType.Addon, 3055)]
    [InlineData(WiredType.Selector, 3058)]
    [InlineData(WiredType.Variable, 3059)]
    public async Task A_refusal_without_a_text_is_answered_with_server_error_1_action_11_E2(
        WiredType kind,
        int messageId
    )
    {
        var packet = (await SaveAsync(kind, WiredSaveResult.ServerError)).Single();

        packet.Header.Should().Be(PacketHarness.Outgoing("ErrorReportMessageComposer"));
        packet.PopInt().Should().Be(messageId);
        packet.PopInt().Should().Be(1);
        DateTime
            .TryParseExact(
                packet.PopString(),
                "M/d/yy, h:mm tt",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _
            )
            .Should()
            .BeTrue();
    }

    [Fact]
    public async Task A_refusal_with_a_text_is_answered_with_the_text()
    {
        var packet = (
            await SaveAsync(
                WiredType.Action,
                WiredSaveResult.RefusedWith("wiredfurni.error.invalid_user_source")
            )
        ).Single();

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredValidationErrorMessageComposer"));
        packet.PopString().Should().Be("wiredfurni.error.invalid_user_source");
    }

    [Fact]
    public async Task A_hotel_without_the_server_error_answers_with_its_own_text()
    {
        var packet = (await SaveAsync(WiredType.Action, WiredSaveResult.Refused)).Single();

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredValidationErrorMessageComposer"));
        packet.PopString().Should().Be(WiredSaveErrors.GENERIC);
    }
}
