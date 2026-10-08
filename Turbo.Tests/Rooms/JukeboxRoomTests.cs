using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Sound.Enums;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.Sound.Snapshots;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A jukebox in a room: the room's music packets reach it without naming it; only its owner
/// changes the playlist; its owner or anyone with rights switches it on and off; and while it
/// plays everyone in the room is told which disk plays and moves on with it when a song ends.
/// </summary>
public sealed class JukeboxRoomTests
{
    private const int JUKEBOX = 7;
    private const int ROOM_OWNER = 1;
    private const int GUEST = 5;

    private const int SONG_A = 21;
    private const int SONG_B = 22;
    private const int SONG_SECONDS = 60;

    private readonly RoomHarness _room = new();
    private readonly RoomFloorItem _jukebox;
    private ImmutableArray<SongDiskSnapshot> _disks =
    [
        new SongDiskSnapshot { DiskId = 100, SongId = SONG_A },
        new SongDiskSnapshot { DiskId = 101, SongId = SONG_B },
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public JukeboxRoomTests()
    {
        var snapshot = (RoomSnapshot)RuntimeHelpers.GetUninitializedObject(typeof(RoomSnapshot));
        RoomHarness.SetMember(snapshot, "RoomId", (RoomId)1);
        RoomHarness.SetMember(snapshot, "OwnerId", (PlayerId)ROOM_OWNER);
        RoomHarness.SetMember(_room.State, "RoomSnapshot", snapshot);
        RoomHarness.SetField(_room.Room, "_dbCtxFactory", new InMemoryDb());

        var stream = typeof(RoomGrain).GetField(
            "_roomOutbound",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
        )!;
        stream.SetValue(_room.Room, _room.Fakes.Create(stream.FieldType, "room-stream"));

        _room.Fakes.Handlers[nameof(IJukeboxGrain.GetDisksAsync)] = _ => Task.FromResult(_disks);
        _room.Fakes.Handlers[nameof(ISongDirectoryGrain.GetSongsAsync)] = call =>
            Task.FromResult<ImmutableArray<SongSnapshot>>([
                .. ((ImmutableArray<int>)call.Args[0]!).Select(Song),
            ]);

        _jukebox = _room.CreateFloorItem(
            JUKEBOX,
            2,
            2,
            Altitude.Zero,
            name: "jukebox*1",
            logic: "jukebox",
            createLogic: (stuffDataFactory, ctx) => new FurnitureJukeboxLogic(stuffDataFactory, ctx)
        );
        _room.AddToRoom(_jukebox);
    }

    [Fact]
    public async Task The_playlist_is_answered_to_whoever_asks_with_the_jukebox_limit()
    {
        (await MusicAsync(GUEST, new RequestJukeboxPlaylistInteraction())).Should().BeTrue();

        var playlist = SentTo(GUEST).OfType<JukeboxSongDisksMessageComposer>().Single();
        playlist.MaxLength.Should().Be(10);
        playlist.Disks.Should().Equal(_disks);
    }

    [Fact]
    public async Task A_room_without_a_jukebox_answers_nothing()
    {
        _room.ItemsById.Remove((RoomObjectId)JUKEBOX);

        (await MusicAsync(GUEST, new RequestNowPlayingInteraction())).Should().BeFalse();
        SentTo(GUEST).Should().BeEmpty();
    }

    [Fact]
    public async Task Only_the_jukebox_owner_changes_its_playlist_not_a_guest_nor_the_room_owner()
    {
        _jukebox.SetOwnerId(9);

        (await MusicAsync(GUEST, new AddJukeboxDiskInteraction { DiskId = 50, Slot = 0 }))
            .Should()
            .BeFalse();
        (await MusicAsync(ROOM_OWNER, new RemoveJukeboxDiskInteraction { Slot = 0 }))
            .Should()
            .BeFalse();

        _room.Fakes.Log.Of(nameof(IJukeboxGrain.AddDiskAsync)).Should().BeEmpty();
        _room.Fakes.Log.Of(nameof(IJukeboxGrain.RemoveDiskAsync)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_disk_the_owner_adds_is_shown_to_the_room_and_a_full_playlist_only_to_them()
    {
        var added = _disks.Add(new SongDiskSnapshot { DiskId = 102, SongId = SONG_A });
        _room.Fakes.Handlers[nameof(IJukeboxGrain.AddDiskAsync)] = _ =>
            Task.FromResult(
                new JukeboxChangeResultSnapshot
                {
                    Result = JukeboxChangeResultType.Done,
                    Disks = added,
                }
            );

        (await MusicAsync(ROOM_OWNER, new AddJukeboxDiskInteraction { DiskId = 102, Slot = 2 }))
            .Should()
            .BeTrue();

        Broadcast().OfType<JukeboxSongDisksMessageComposer>().Single().Disks.Should().Equal(added);

        _room.Fakes.Handlers[nameof(IJukeboxGrain.AddDiskAsync)] = _ =>
            Task.FromResult(
                new JukeboxChangeResultSnapshot
                {
                    Result = JukeboxChangeResultType.Full,
                    Disks = added,
                }
            );

        (await MusicAsync(ROOM_OWNER, new AddJukeboxDiskInteraction { DiskId = 103, Slot = 3 }))
            .Should()
            .BeFalse();

        SentTo(ROOM_OWNER).OfType<JukeboxPlayListFullMessageComposer>().Should().ContainSingle();
        Broadcast().OfType<JukeboxSongDisksMessageComposer>().Should().ContainSingle();
    }

    [Fact]
    public async Task Switched_on_it_plays_the_chosen_disk_for_the_room_and_moves_on_when_it_ends()
    {
        (await UseAsync(ROOM_OWNER, param: 1)).Should().BeTrue();

        _jukebox.Logic.GetState().Should().Be(JukeboxStates.ON);
        var playing = Broadcast().OfType<NowPlayingMessageComposer>().Last();
        playing.CurrentSongId.Should().Be(SONG_B);
        playing.CurrentPosition.Should().Be(1);
        playing.NextSongId.Should().Be(SONG_A);
        playing.NextPosition.Should().Be(0);

        var before = Broadcast().OfType<NowPlayingMessageComposer>().Count();

        // The room's clock at the moment the song is due to end, and not past it.
        await _room.Module<RoomTimerSystem>().ProcessTimersAsync(SongEndsAtMs(), Ct);

        var next = Broadcast().OfType<NowPlayingMessageComposer>().Skip(before).First();
        next.CurrentSongId.Should().Be(SONG_A);
        next.CurrentPosition.Should().Be(0);

        // A late arrival asks what plays and joins it.
        await MusicAsync(GUEST, new RequestNowPlayingInteraction());
        SentTo(GUEST)
            .OfType<NowPlayingMessageComposer>()
            .Single()
            .CurrentSongId.Should()
            .Be(Broadcast().OfType<NowPlayingMessageComposer>().Last().CurrentSongId);

        (await UseAsync(ROOM_OWNER, param: -1)).Should().BeTrue();

        _jukebox.Logic.GetState().Should().Be(JukeboxStates.OFF);
        Broadcast()
            .OfType<NowPlayingMessageComposer>()
            .Last()
            .Should()
            .Be(NowPlayingMessageComposer.Nothing());
    }

    [Fact]
    public async Task A_guest_without_rights_cannot_switch_it_on()
    {
        (await UseAsync(GUEST, param: 0)).Should().BeFalse();

        _jukebox.Logic.GetState().Should().Be(JukeboxStates.OFF);
        Broadcast().OfType<NowPlayingMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Taking_out_the_disk_that_plays_starts_the_one_after_it()
    {
        await UseAsync(ROOM_OWNER, param: 0);
        _room.Fakes.Handlers[nameof(IJukeboxGrain.RemoveDiskAsync)] = _ =>
            Task.FromResult(
                new JukeboxChangeResultSnapshot
                {
                    Result = JukeboxChangeResultType.Done,
                    Disks = [_disks[1]],
                }
            );

        (await MusicAsync(ROOM_OWNER, new RemoveJukeboxDiskInteraction { Slot = 0 }))
            .Should()
            .BeTrue();

        var playing = Broadcast().OfType<NowPlayingMessageComposer>().Last();
        playing.CurrentSongId.Should().Be(SONG_B);
        playing.CurrentPosition.Should().Be(0);
    }

    [Fact]
    public async Task An_empty_jukebox_does_not_switch_on()
    {
        _disks = [];

        await UseAsync(ROOM_OWNER, param: 0);

        _jukebox.Logic.GetState().Should().Be(JukeboxStates.OFF);
        Broadcast().OfType<NowPlayingMessageComposer>().Should().BeEmpty();
    }

    /// <summary>When the jukebox's pending timer, the end of the song playing, is due.</summary>
    private long SongEndsAtMs()
    {
        const System.Reflection.BindingFlags ALL =
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic;

        var timers = (System.Collections.IDictionary)
            RoomHarness.GetField(_room.Module<RoomTimerSystem>(), "_timersByObjectId")!;
        var timer = timers[(RoomObjectId)JUKEBOX]!;

        return (long)timer.GetType().GetProperty("DueAtMs", ALL)!.GetValue(timer)!;
    }

    private Task<bool> MusicAsync(int playerId, FurnitureInteraction interaction) =>
        _room.Room.InteractWithMusicPlayerAsync(
            ActionContext.CreateForPlayer(playerId, 1),
            interaction,
            Ct
        );

    private Task<bool> UseAsync(int playerId, int param) =>
        _room.Room.UseItemByIdAsync(ActionContext.CreateForPlayer(playerId, 1), JUKEBOX, Ct, param);

    private IEnumerable<IComposer> Broadcast() =>
        _room
            .Fakes.Log.Of("OnNextAsync")
            .SelectMany(call => ((RoomOutboundSnapshot)call.Args[0]!).Composers);

    private IEnumerable<IComposer> SentTo(int playerId) =>
        _room
            .Fakes.Log.Of("SendComposerAsync")
            .Where(call => Equals(call.Key, (long)playerId))
            .SelectMany(call => call.Args.OfType<IComposer>());

    private static SongSnapshot Song(int id) =>
        new()
        {
            Id = id,
            Code = string.Empty,
            Name = $"song {id}",
            Author = "staff",
            Track = "1:0,4",
            LengthSeconds = SONG_SECONDS,
            IsOfficial = true,
        };
}
