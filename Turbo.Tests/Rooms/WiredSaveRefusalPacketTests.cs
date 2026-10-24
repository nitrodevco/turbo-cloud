using System.Globalization;
using FluentAssertions;
using Turbo.LoadBots.Protocol;
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
        WiredKind kind,
        WiredSaveResult result
    )
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers["ApplyWiredUpdateAsync"] = _ => Task.FromResult(result);

        var save = ClientRequests.UpdateWired(new WiredSave { Kind = kind, ObjectId = 7 });

        return await harness.SendAsync(save.Header, save.Body, roomId: 5);
    }

    [Theory]
    [InlineData(WiredKind.Trigger, 3050)]
    [InlineData(WiredKind.Action, 3051)]
    [InlineData(WiredKind.Condition, 3052)]
    [InlineData(WiredKind.Addon, 3055)]
    [InlineData(WiredKind.Selector, 3058)]
    [InlineData(WiredKind.Variable, 3059)]
    public async Task A_refusal_without_a_text_is_answered_with_server_error_1_action_11_E2(
        WiredKind kind,
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
                WiredKind.Action,
                WiredSaveResult.RefusedWith("wiredfurni.error.invalid_user_source")
            )
        ).Single();

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredValidationErrorMessageComposer"));
        packet.PopString().Should().Be("wiredfurni.error.invalid_user_source");
    }

    [Fact]
    public async Task A_hotel_without_the_server_error_answers_with_its_own_text()
    {
        var packet = (await SaveAsync(WiredKind.Action, WiredSaveResult.Refused)).Single();

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredValidationErrorMessageComposer"));
        packet.PopString().Should().Be(WiredSaveErrors.GENERIC);
    }
}
