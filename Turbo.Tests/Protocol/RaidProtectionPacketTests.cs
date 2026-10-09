using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// Raid protection's packets in the AS3's field order: <c>RaidProtectionSettingsSnapshot</c>
/// (room id, enabled, detection sensitivity, action, ban seconds, guard enabled, guard seconds,
/// guard sensitivity, incident active, last raid), the result with its code after the room id,
/// the capability (room id, can manage) and the save composer (the editable fields, then
/// confirmed).
/// </summary>
public class RaidProtectionPacketTests
{
    private static readonly RaidProtectionSettingsSnapshot Settings = new()
    {
        RoomId = 7,
        Enabled = true,
        DetectionSensitivity = RaidSensitivityType.High,
        ActionType = RaidActionType.TemporaryBan,
        BanDurationSeconds = 3600,
        GuardEnabled = true,
        GuardDurationSeconds = 300,
        GuardSensitivity = RaidSensitivityType.Low,
        IncidentActive = false,
        LastRaidAtEpochSeconds = 1_760_000_000,
    };

    [Fact]
    public void The_settings_are_written_as_the_client_reads_them()
    {
        var reply = PacketHarness.Encode(
            new RaidProtectionSettingsMessageComposer { Settings = Settings }
        );

        Assert.Equal(7, reply.PopInt());
        AssertAfterRoomId(reply);
    }

    [Fact]
    public void The_result_puts_its_code_after_the_room_id()
    {
        var reply = PacketHarness.Encode(
            new RaidProtectionSettingsResultMessageComposer
            {
                Result = new RaidProtectionSaveResultSnapshot
                {
                    Result = RaidProtectionSaveResultType.NotConfirmed,
                    Settings = Settings,
                },
            }
        );

        Assert.Equal(7, reply.PopInt());
        Assert.Equal(5, reply.PopInt());
        AssertAfterRoomId(reply);
    }

    [Fact]
    public void The_capability_is_the_room_and_whether_the_player_may_manage_it()
    {
        var reply = PacketHarness.Encode(
            new RaidProtectionCapabilityMessageComposer { RoomId = 7, CanManage = true }
        );

        Assert.Equal(7, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.True(reply.End);
    }

    [Fact]
    public void A_save_is_read_in_the_order_the_client_sends_it()
    {
        var message = (SaveRaidProtectionSettingsMessage)
            PacketHarness.Parse(
                PacketHarness.Incoming("SaveRaidProtectionSettingsMessageEvent"),
                PacketHarness.Payload(w =>
                    w.Int(7)
                        .Bool(true)
                        .Int(1)
                        .Int(1)
                        .Int(900)
                        .Bool(false)
                        .Int(1800)
                        .Int(2)
                        .Bool(true)
                )
            );

        Assert.Equal(7, message.RoomId.Value);
        Assert.Equal(
            new RaidProtectionSettingsUpdateSnapshot
            {
                Enabled = true,
                DetectionSensitivity = 1,
                ActionType = 1,
                BanDurationSeconds = 900,
                GuardEnabled = false,
                GuardDurationSeconds = 1800,
                GuardSensitivity = 2,
                Confirmed = true,
            },
            message.Settings
        );
    }

    private static void AssertAfterRoomId(Turbo.Primitives.Packets.ClientPacket reply)
    {
        Assert.True(reply.PopBoolean());
        Assert.Equal(2, reply.PopInt());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(3600, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.Equal(300, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.False(reply.PopBoolean());
        Assert.Equal(1_760_000_000, reply.PopInt());
        Assert.True(reply.End);
    }
}
