using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The help window's "sanction information" asks <c>GetMySanctionStatus</c>. The client's
/// <c>SanctionStatusEvent</c> parser reads a count, then per sanction its type (name, hours, an
/// unread int), the text shown, whether it is gradual, the probation hours left and the next
/// type. With none it shows <c>settings.help.sanction_information.description</c>. A signed-in
/// player cannot be banned, so what can be in force is a silence or a trade lock: a denial of
/// <c>chat.speak</c> or <c>trade</c>.
/// </summary>
public class SanctionStatusTests
{
    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task a_player_under_no_sanction_is_sent_an_empty_list()
    {
        var reply = await AskAsync(speak: Granted(), trade: Granted());

        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.End);
    }

    [Fact]
    public async Task a_silence_for_a_time_is_listed_as_a_mute_with_its_end()
    {
        var reply = await AskAsync(speak: Denied(NOW.AddHours(5).AddMinutes(10)), trade: Granted());

        Assert.Equal(1, reply.PopInt());
        Assert.Equal("MUTE", reply.PopString());
        Assert.Equal(6, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal("You can't speak in the hotel until 2026-10-09 17:10 UTC.", reply.PopString());
        Assert.False(reply.PopBoolean());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal(string.Empty, reply.PopString());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.End);
    }

    [Fact]
    public async Task a_trade_lock_for_good_is_listed_with_no_end()
    {
        var reply = await AskAsync(speak: Granted(), trade: Denied(endsAt: null));

        Assert.Equal(1, reply.PopInt());
        Assert.Equal("TRADE_LOCK", reply.PopString());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal("You can't trade.", reply.PopString());
    }

    [Fact]
    public async Task a_node_nobody_granted_is_not_a_sanction()
    {
        var reply = await AskAsync(
            speak: Granted() with
            {
                Granted = false,
                Decision = null,
            },
            trade: Granted()
        );

        Assert.Equal(0, reply.PopInt());
    }

    private static async Task<Turbo.Primitives.Packets.ClientPacket> AskAsync(
        PermissionCheckSnapshot speak,
        PermissionCheckSnapshot trade
    )
    {
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(TimeProvider)] = new ManualTimeProvider(
            new DateTimeOffset(NOW)
        );
        harness.Resolver.Overrides[typeof(IHotelTextProvider)] =
            harness.Fakes.Create<IHotelTextProvider>();
        harness.Fakes.Handlers["ExplainAsync"] = call =>
            Task.FromResult(
                (string)call.Args[0]! == PermissionNodes.Chat.SPEAK
                    ? speak with
                    {
                        Node = PermissionNodes.Chat.SPEAK,
                    }
                    : trade with
                    {
                        Node = PermissionNodes.TRADE,
                    }
            );

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetMySanctionStatusMessageEvent"),
            []
        );

        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("SanctionStatusMessageComposer"), reply.Header);
        return reply;
    }

    private static PermissionCheckSnapshot Granted() =>
        new()
        {
            Node = string.Empty,
            IsRegistered = true,
            Granted = true,
            Decision = null,
            Overridden = [],
        };

    private static PermissionCheckSnapshot Denied(DateTime? endsAt) =>
        Granted() with
        {
            Granted = false,
            Decision = new PermissionAssignmentSourceSnapshot
            {
                SourceType = PermissionSourceType.Player,
                Path = [],
                Node = string.Empty,
                Value = false,
                ExpiresAt = endsAt,
            },
        };
}
