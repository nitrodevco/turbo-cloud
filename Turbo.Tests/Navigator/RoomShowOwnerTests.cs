using FluentAssertions;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Navigator;

/// <summary>
/// GuestRoomData's show-owner bit (8, <c>GuestRoomData.as:116</c>): Habbo leaves it off for its
/// official rooms (official_rooms, 84 of 84 recorded rooms, official client capture,
/// WIN63-202610131318), and the client then hides the owner on the room card, the room info
/// and the room tools (<c>RoomInfoPopup.as:164</c>, <c>RoomToolsWidgetHandler.as:51</c>). Turbo
/// set it for every room.
/// </summary>
public class RoomShowOwnerTests
{
    private const int SHOW_OWNER = 8;

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void A_room_shows_its_owner_unless_it_hides_them(bool hideOwner, bool shown)
    {
        var room = Room(hideOwner).ToSnapshot("official_rooms", "model_a", null, DateTime.UtcNow);
        var reply = PacketHarness.Encode(
            new GetGuestRoomResultMessageComposer
            {
                EnterRoom = false,
                RoomInfo = room,
                RoomForward = false,
                StaffPick = false,
                IsGroupMember = false,
                AllInRoomMuted = false,
                CanMute = false,
                OpeningConnection = false,
            }
        );

        reply.PopBoolean();
        reply.PopInt();
        reply.PopString();
        reply.PopInt();
        reply.PopString().Should().Be("official_rooms");
        for (var i = 0; i < 3; i++)
            reply.PopInt();
        reply.PopString();
        for (var i = 0; i < 4; i++)
            reply.PopInt();
        reply.PopInt().Should().Be(0, "the room has no tags");

        ((reply.PopInt() & SHOW_OWNER) != 0).Should().Be(shown);
    }

    private static RoomEntity Room(bool hideOwner) =>
        new()
        {
            Id = 7,
            Name = "Welcome Lounge",
            PlayerEntityId = 1,
            RoomModelEntityId = 1,
            DoorMode = RoomDoorModeType.Open,
            UsersNow = 0,
            PlayersMax = 25,
            WallHeight = -1,
            HideWalls = false,
            ThicknessWall = RoomThicknessType.Normal,
            ThicknessFloor = RoomThicknessType.Normal,
            AllowBlocking = false,
            AllowPets = false,
            AllowPetsEat = false,
            TradeType = RoomTradeModeType.Disabled,
            MuteType = ModSettingType.Owner,
            KickType = ModSettingType.Owner,
            BanType = ModSettingType.Owner,
            ChatFloodType = ChatFloodSensitivityType.Minimal,
            HideOwner = hideOwner,
            PlayerEntity = null!,
            RoomModelEntity = null!,
        };
}
