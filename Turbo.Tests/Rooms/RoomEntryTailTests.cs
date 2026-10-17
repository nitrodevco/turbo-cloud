using System.Text.Json;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Mapping;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The end of a room entry, in Habbo's order (official client captures, 32 of 32 entries on
/// builds 1709, 1318 and 1745): ..., Items, the player's own Users, RoomEntryInfo, RoomEvent,
/// ConfigurationItemStates, WiredEnvironment, FurnitureLabelsAndRegions, YouAreController,
/// WiredPermissions, YouAreOwner, RoomRating, RaidProtectionCapability. Turbo sent RoomEntryInfo
/// last, YouAreOwner before WiredPermissions, and the player's own avatar through the room stream
/// after all of it.
/// </summary>
public sealed class RoomEntryTailTests
{
    private const int OWNER = 1;
    private const int VISITOR = 42;
    private const int SHOW_MESSAGE = 7;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public RoomEntryTailTests()
    {
        var fakes = _room.Harness.Fakes;

        fakes.Handlers["GetResolvedAsync"] = _ =>
            Task.FromResult(ResolvedPermissionsSnapshot.EMPTY);
        fakes.Handlers["CreateAvatarFromPlayerSnapshot"] = call => new RoomPlayerAvatar
        {
            ObjectId = (RoomObjectId)call.Args[0]!,
            PlayerId = ((PlayerSummarySnapshot)call.Args[1]!).PlayerId,
        };
    }

    [Fact]
    public async Task The_owner_is_told_the_rest_of_the_entry_in_Habbos_order()
    {
        var tail = await EnterAsync(OWNER);

        tail.Select(x => x.GetType())
            .Should()
            .Equal(
                typeof(UsersMessageComposer),
                typeof(RoomEntryInfoMessageComposer),
                typeof(RoomEventMessageComposer),
                typeof(ConfigurationItemStatesMessageComposer),
                typeof(WiredEnvironmentMessageComposer),
                typeof(FurnitureLabelsAndRegionsMessageComposer),
                typeof(YouAreControllerMessageComposer),
                typeof(WiredPermissionsEventMessageComposer),
                typeof(YouAreOwnerMessageComposer),
                typeof(RoomRatingMessageComposer),
                typeof(RaidProtectionCapabilityMessageComposer),
                typeof(UserUpdateMessageComposer)
            );
        tail.OfType<UsersMessageComposer>()
            .Single()
            .Avatars.Should()
            .ContainSingle()
            .Which.WebId.Should()
            .Be(OWNER);
        tail.OfType<RoomEntryInfoMessageComposer>().Single().IsOwner.Should().BeTrue();
        tail.OfType<WiredPermissionsEventMessageComposer>()
            .Single()
            .Should()
            .Match<WiredPermissionsEventMessageComposer>(x => x.CanModify && x.CanRead);
        tail.OfType<RaidProtectionCapabilityMessageComposer>().Single().CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task A_visitor_without_rights_is_told_nothing_of_control()
    {
        // 6 of 6 such official entries: no YouAreController, YouAreNotController or YouAreOwner.
        var tail = await EnterAsync(VISITOR);

        tail.Should().NotContain(x => x is YouAreControllerMessageComposer);
        tail.Should().NotContain(x => x is YouAreNotControllerMessageComposer);
        tail.Should().NotContain(x => x is YouAreOwnerMessageComposer);
        tail.OfType<RoomEntryInfoMessageComposer>().Single().IsOwner.Should().BeFalse();
        tail.OfType<RaidProtectionCapabilityMessageComposer>()
            .Single()
            .CanManage.Should()
            .BeFalse();
    }

    [Fact]
    public async Task The_event_and_configuration_states_are_sent_even_when_none()
    {
        // Habbo, entering a room with no event and no configuration furni on: an empty
        // RoomEvent (ids -1) and ConfigurationItemStates all off, so nothing of the room before
        // is kept.
        var tail = await EnterAsync(VISITOR);

        tail.OfType<RoomEventMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Event.Should()
            .BeNull();
        tail.OfType<ConfigurationItemStatesMessageComposer>()
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    IsHanditemControlBlocked = false,
                    ChooserDisabled = false,
                    InvisibleFurni = false,
                }
            );
    }

    [Fact]
    public async Task The_player_gets_their_own_avatar_in_the_entry_and_everyone_else_through_the_room()
    {
        await EnterAsync(VISITOR);

        var broadcast = _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .Select(call => (RoomOutboundSnapshot)call.Args[0]!)
            .Single(x => x.Composers.Any(c => c is UsersMessageComposer));

        broadcast.ExcludedPlayerIds.Should().Equal((PlayerId)VISITOR);
    }

    [Fact]
    public async Task What_the_others_are_doing_follows_the_raid_protection()
    {
        // No UserUpdate before RaidProtectionCapability in any official entry (0 of 32).
        _room.Enter(5, 3, 3);

        var tail = (await EnterAsync(VISITOR)).ToList();
        var update = tail.OfType<UserUpdateMessageComposer>().Should().ContainSingle().Subject;

        tail.IndexOf(update)
            .Should()
            .BeGreaterThan(tail.FindIndex(x => x is RaidProtectionCapabilityMessageComposer));
        // Own status first, then the others' (official duo entries, 2026-10-13).
        var own = tail.OfType<UsersMessageComposer>().Single().Avatars.Single().ObjectId;
        update.Avatars.Select(x => x.ObjectId).Should().Equal(own, (RoomObjectId)5);
    }

    [Theory]
    [InlineData(Rotation.East)]
    [InlineData(Rotation.South)]
    [InlineData(Rotation.West)]
    public async Task The_player_walking_in_faces_the_doors_direction_for_themselves_and_the_others(
        Rotation door
    )
    {
        // Bill, 2026-10-17: the avatar stood in the door facing the wall. Habbo sends the door's
        // direction in Users and again in the UserUpdate after the raid protection (53 of 54
        // official entries); the client takes the Users one as an angle, so only the update turns it.
        var model = (RoomModelSnapshot)RoomHarness.GetMember(_room.Harness.State, "Model")!;
        RoomHarness.SetMember(
            _room.Harness.State,
            "Model",
            model with
            {
                DoorX = 0,
                DoorY = 3,
                DoorRotation = door,
            }
        );
        _room.Enter(5, 3, 3);

        var tail = await EnterAsync(VISITOR);

        tail.OfType<UsersMessageComposer>()
            .Single()
            .Avatars.Single()
            .BodyRotation.Should()
            .Be(door);
        tail.OfType<UserUpdateMessageComposer>()
            .Single()
            .Avatars[0]
            .Should()
            .Match<RoomAvatarSnapshot>(x =>
                x.BodyRotation == door && x.HeadRotation == door && x.X == 0 && x.Y == 3
            );

        // Everyone already here: the Users that draws the newcomer, then their status.
        var toOthers = _room
            .Harness.Fakes.Log.Of("OnNextAsync")
            .Select(call => (RoomOutboundSnapshot)call.Args[0]!)
            .Where(x => x.ExcludedPlayerIds?.Contains((PlayerId)VISITOR) == true)
            .SelectMany(x => x.Composers)
            .ToList();

        toOthers
            .Select(x => x.GetType())
            .Should()
            .Equal(typeof(UsersMessageComposer), typeof(UserUpdateMessageComposer));
        toOthers
            .OfType<UsersMessageComposer>()
            .Single()
            .Avatars.Single()
            .BodyRotation.Should()
            .Be(door);
        toOthers
            .OfType<UserUpdateMessageComposer>()
            .Single()
            .Avatars.Should()
            .ContainSingle()
            .Which.BodyRotation.Should()
            .Be(door);
    }

    [Theory]
    [InlineData(OWNER)]
    [InlineData(VISITOR)]
    public async Task A_room_whose_wired_writes_the_trigger_text_says_so_right_after_the_entry_info(
        int playerId
    )
    {
        // Official client captures (18 of 18 entries into a room whose Show Message held
        // $(trigger_text), builds 1709 and 1318): NotificationDialog wired.error between
        // RoomEntryInfo and RoomEvent. Turbo never sent it.
        await SaveShowMessageAsync("$(trigger_text)[b] said it");

        var tail = (await EnterAsync(playerId)).ToList();
        var notice = tail.OfType<NotificationDialogMessageComposer>()
            .Should()
            .ContainSingle()
            .Subject;

        tail.IndexOf(notice)
            .Should()
            .Be(tail.FindIndex(x => x is RoomEntryInfoMessageComposer) + 1)
            .And.Be(tail.FindIndex(x => x is RoomEventMessageComposer) - 1);
        notice.NotificationType.Should().Be("wired.error");
        notice
            .Parameters.Should()
            .Equal(
                new Dictionary<string, string>
                {
                    ["image"] = "${image.library.url}album1584/WIRD1.png",
                    ["message"] = "${room.error.wired.trigger_text_capture}",
                }
            );
    }

    [Fact]
    public async Task A_wired_text_without_the_trigger_text_brings_no_notice()
    {
        // Saved as "parity test", the next official entry came without it (13:10:11); put back,
        // the one after had it again (13:10:29).
        await SaveShowMessageAsync("parity test");

        var tail = await EnterAsync(OWNER);

        tail.Should().NotContain(x => x is NotificationDialogMessageComposer);
    }

    [Theory]
    [InlineData("$(trigger_text)[b] said it", true)]
    [InlineData("parity test", false)]
    public async Task The_first_entry_into_a_room_just_woken_reads_the_boxes_the_wired_tick_has_not_loaded(
        string text,
        bool told
    )
    {
        // A woken room's boxes load on the wired tick; an entry before it read a box with no data
        // and lost the whole entry ("Failed to create avatar": the player stood in an empty room).
        StoreShowMessage(text);

        var tail = await EnterAsync(OWNER);

        tail.Should().Contain(x => x is RoomEntryInfoMessageComposer);
        tail.Any(x => x is NotificationDialogMessageComposer).Should().Be(told);
    }

    [Fact]
    public async Task A_muted_room_is_not_told_as_mute_all_in_the_entry()
    {
        // Official client capture 2026-10-16T04-50-48 (build 1745): owner muted the room, left and
        // came back; no MuteAllInRoom in the entry, the GetGuestRoomResult the client asks for
        // carried allInRoomMuted.
        await _room.Harness.Module<RoomModerationModule>().SetRoomMutedBySystemAsync(true, Ct);

        var tail = await EnterAsync(OWNER);

        tail.Should().NotContain(x => x is MuteAllInRoomEventMessageComposer);
    }

    private async Task SaveShowMessageAsync(string text)
    {
        _room.AddBox<WiredActionShowMessage>(SHOW_MESSAGE, 0, 0, "wf_act_show_message");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                SHOW_MESSAGE,
                intParams: [1, 34, -1],
                stringParam: text,
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();
    }

    /// <summary>A Show Message saved with <paramref name="text"/> that the wired tick has not loaded yet.</summary>
    private void StoreShowMessage(string text)
    {
        _room.AddBox<WiredActionShowMessage>(SHOW_MESSAGE, 0, 0, "wf_act_show_message");
        _room
            .FloorItem(SHOW_MESSAGE)
            .ExtraData.UpdateSection(
                ExtraDataSectionType.WIRED,
                JsonSerializer.SerializeToNode(new WiredData { StringParam = text })!
            );
    }

    private async Task<IReadOnlyList<IComposer>> EnterAsync(int playerId) =>
        await _room.Harness.Room.CreateAvatarFromPlayerAsync(
            ActionContext.CreateForPlayer((PlayerId)playerId, (RoomId)1),
            new PlayerSummarySnapshot
            {
                PlayerId = (PlayerId)playerId,
                Name = "player",
                Motto = string.Empty,
                Figure = string.Empty,
                Gender = default,
                AchievementScore = 0,
                BadgesRank = 0,
                IsOnline = true,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow,
                RespectPoints = 0,
                RespectsLeft = 0,
                PetRespectsLeft = 0,
                RespectReplenishesLeft = 0,
            },
            new RoomEntrySnapshot { Method = RoomEntryMethodType.Default, TeleportId = 0 },
            Ct
        );
}
