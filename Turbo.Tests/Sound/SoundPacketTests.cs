using System.Collections.Immutable;
using System.Globalization;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Incoming.Sound;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Sound.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Sound;

/// <summary>
/// The music packets as the client reads and writes them, field for field from its parsers
/// (<c>TraxSongInfoMessageParser</c>, <c>JukeboxSongDisksMessageParser</c>,
/// <c>NowPlayingMessageParser</c> and the others), and the song id a disk shows wherever the
/// client reads it: beside the item in the inventory and in the room.
/// </summary>
public sealed class SoundPacketTests
{
    private const int SONG = 42;

    private static readonly SongSnapshot Theme = new()
    {
        Id = SONG,
        Code = "theme_1",
        Name = "Theme",
        Author = "Staff",
        Track = "1:0,4;2:0,4",
        LengthSeconds = 95,
        IsOfficial = true,
    };

    [Fact]
    public void The_requests_read_what_the_client_sends()
    {
        PacketHarness
            .Parse(
                PacketHarness.Incoming("GetSongInfoMessageEvent"),
                PacketHarness.Payload(w => w.Int(2).Int(SONG).Int(7))
            )
            .Should()
            .BeOfType<GetSongInfoMessage>()
            .Which.SongIds.Should()
            .Equal(SONG, 7);
        PacketHarness
            .Parse(
                PacketHarness.Incoming("AddJukeboxDiskMessageEvent"),
                PacketHarness.Payload(w => w.Int(100).Int(3))
            )
            .Should()
            .BeEquivalentTo(new AddJukeboxDiskMessage { DiskId = 100, Slot = 3 });
        PacketHarness
            .Parse(
                PacketHarness.Incoming("RemoveJukeboxDiskMessageEvent"),
                PacketHarness.Payload(w => w.Int(2))
            )
            .Should()
            .BeEquivalentTo(new RemoveJukeboxDiskMessage { Slot = 2 });
        PacketHarness
            .Parse(
                PacketHarness.Incoming("GetOfficialSongIdMessageEvent"),
                PacketHarness.Payload(w => w.String("theme_1"))
            )
            .Should()
            .BeEquivalentTo(new GetOfficialSongIdMessage { Code = "theme_1" });
    }

    [Fact]
    public async Task Song_info_is_id_code_name_track_length_in_ms_and_author_of_known_songs()
    {
        var harness = new PacketHarness();
        harness.Fakes.Handlers["GetSongsAsync"] = call =>
            Task.FromResult<ImmutableArray<SongSnapshot>>([
                .. ((ImmutableArray<int>)call.Args[0]!).Where(id => id == SONG).Select(_ => Theme),
            ]);

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetSongInfoMessageEvent"),
            PacketHarness.Payload(w => w.Int(2).Int(SONG).Int(999))
        );

        var packet = replies.Should().ContainSingle().Subject;
        packet.Header.Should().Be(PacketHarness.Outgoing("TraxSongInfoMessageComposer"));
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(SONG);
        packet.PopString().Should().Be("theme_1");
        packet.PopString().Should().Be("Theme");
        packet.PopString().Should().Be("1:0,4;2:0,4");
        packet.PopInt().Should().Be(95_000);
        packet.PopString().Should().Be("Staff");
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task An_official_song_code_is_answered_with_its_id_and_an_unknown_one_not_at_all()
    {
        var harness = new PacketHarness();
        harness.Fakes.Handlers["GetSongIdByCodeAsync"] = call =>
            Task.FromResult<int?>((string)call.Args[0]! == "theme_1" ? SONG : null);

        var known = await harness.SendAsync(
            PacketHarness.Incoming("GetOfficialSongIdMessageEvent"),
            PacketHarness.Payload(w => w.String("theme_1"))
        );

        var packet = known.Should().ContainSingle().Subject;
        packet.Header.Should().Be(PacketHarness.Outgoing("OfficialSongIdMessageComposer"));
        packet.PopString().Should().Be("theme_1");
        packet.PopInt().Should().Be(SONG);
        packet.Remaining.Should().Be(0);

        harness.Sent.Clear();

        (
            await harness.SendAsync(
                PacketHarness.Incoming("GetOfficialSongIdMessageEvent"),
                PacketHarness.Payload(w => w.String("nothing"))
            )
        )
            .Should()
            .BeEmpty();
    }

    [Fact]
    public async Task A_players_song_disks_are_the_trax_song_furni_of_their_inventory()
    {
        var harness = new PacketHarness();
        harness.Fakes.Handlers["GetAllItemSnapshotsAsync"] = _ =>
            Task.FromResult<ImmutableArray<FurnitureItemSnapshot>>([
                Item(10, FurnitureCategory.TraxSong, extra: SONG),
                Item(11, FurnitureCategory.Default, extra: 0),
            ]);

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetUserSongDisksMessageEvent"),
            []
        );

        var packet = replies.Should().ContainSingle().Subject;
        packet.Header.Should().Be(PacketHarness.Outgoing("UserSongDisksInventoryMessageComposer"));
        packet.PopInt().Should().Be(1);
        packet.PopInt().Should().Be(10);
        packet.PopInt().Should().Be(SONG);
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task A_jukebox_packet_goes_to_the_room_the_player_is_in()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("AddJukeboxDiskMessageEvent"),
            PacketHarness.Payload(w => w.Int(100).Int(3)),
            playerId: 4,
            roomId: 9
        );

        var call = harness
            .Fakes.Log.Of("InteractWithMusicPlayerAsync")
            .Should()
            .ContainSingle()
            .Subject;
        call.Key.Should().Be(9L);
        ((ActionContext)call.Args[0]!).PlayerId.Value.Should().Be(4);
        call.Args[1].Should().Be(new AddJukeboxDiskInteraction { DiskId = 100, Slot = 3 });
    }

    [Fact]
    public void The_playlist_now_playing_and_sound_machine_list_are_written_as_the_client_reads_them()
    {
        var disks = PacketHarness.Encode(
            new JukeboxSongDisksMessageComposer
            {
                MaxLength = 10,
                Disks = [new SongDiskSnapshot { DiskId = 100, SongId = SONG }],
            }
        );
        disks.Header.Should().Be(PacketHarness.Outgoing("JukeboxSongDisksMessageComposer"));
        Ints(disks, 4).Should().Equal(10, 1, 100, SONG);
        disks.Remaining.Should().Be(0);

        var playing = PacketHarness.Encode(
            new NowPlayingMessageComposer
            {
                CurrentSongId = SONG,
                CurrentPosition = 1,
                NextSongId = 7,
                NextPosition = 0,
                SyncCountMs = 1500,
            }
        );
        playing.Header.Should().Be(PacketHarness.Outgoing("NowPlayingMessageComposer"));
        Ints(playing, 5).Should().Equal(SONG, 1, 7, 0, 1500);
        playing.Remaining.Should().Be(0);

        var list = PacketHarness.Encode(
            new PlayListMessageComposer { SynchronizationCountMs = 2000, Songs = [Theme] }
        );
        list.Header.Should().Be(PacketHarness.Outgoing("PlayListMessageComposer"));
        Ints(list, 4).Should().Equal(2000, 1, SONG, 95_000);
        list.PopString().Should().Be("Theme");
        list.PopString().Should().Be("Staff");
        list.Remaining.Should().Be(0);

        var full = PacketHarness.Encode(new JukeboxPlayListFullMessageComposer());
        full.Header.Should().Be(PacketHarness.Outgoing("JukeboxPlayListFullMessageComposer"));
        full.Remaining.Should().Be(0);
    }

    [Fact]
    public void A_disk_standing_in_a_room_shows_its_song_as_the_objects_extras()
    {
        var room = new RoomHarness();
        var disk = room.CreateFloorItem(
            30,
            1,
            1,
            Altitude.Zero,
            name: "song_disk",
            logic: "song_disk",
            createLogic: (stuffDataFactory, ctx) =>
            {
                ctx.RoomObject.SetExtraData(
                    ProductStuffData.ExtraData(SONG.ToString(CultureInfo.InvariantCulture))
                );

                return new FurnitureSongDiskLogic(stuffDataFactory, ctx);
            }
        );

        var packet = PacketHarness.Encode(
            new ObjectAddMessageComposer { FloorItem = disk.GetSnapshot() }
        );

        packet.PopInt().Should().Be(30); // id
        packet.PopInt(); // sprite
        packet.PopInt(); // x
        packet.PopInt(); // y
        packet.PopInt(); // direction
        packet.PopString(); // z
        packet.PopString(); // stack height
        packet.PopInt().Should().Be(SONG);
    }

    private static List<int> Ints(Turbo.Primitives.Packets.ClientPacket packet, int count) =>
        [.. Enumerable.Range(0, count).Select(_ => packet.PopInt())];

    private static FurnitureItemSnapshot Item(int id, FurnitureCategory category, int extra) =>
        new()
        {
            ItemId = id,
            SpriteId = 2607,
            OwnerId = 1,
            OwnerName = "owner",
            Definition = new FurnitureDefinitionSnapshot
            {
                Id = 1000 + id,
                SpriteId = 2607,
                Name = "song_disk",
                ProductType = ProductType.Floor,
                FurniCategory = category,
                LogicName = "default_floor",
                TotalStates = 0,
                Width = 1,
                Length = 1,
                StackHeight = Altitude.Zero,
                CanStack = true,
                CanWalk = false,
                CanSit = false,
                CanLay = false,
                CanRecycle = false,
                CanTrade = true,
                CanGroup = true,
                CanSell = true,
                UsagePolicy = FurnitureUsageType.Nobody,
                ExtraData = null,
            },
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "" },
            ExtraData = "",
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
            Extra = extra,
        };
}
