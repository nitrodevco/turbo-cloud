using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Sound.Enums;
using Turbo.Primitives.Sound.Grains;
using Turbo.Primitives.Sound.Snapshots;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A jukebox: song disks in a playlist, played in turn for the whole room. The disks are its
/// grain's (<see cref="IJukeboxGrain"/>); this logic keeps the clock. The client plays each song
/// itself and never says when one ends, so the room says which disk plays and how far in it is
/// (<c>NowPlaying</c>), and moves on to the next when the song's length is up, going round the
/// playlist until it is switched off. The state is <see cref="JukeboxStates.ON"/> while it plays,
/// and a jukebox that was playing when its room unloaded starts its playlist over when it loads.
/// <para>
/// Who may do what follows the client: only the jukebox's owner gets the playlist editor, so
/// only they add and take out disks; they and anyone with rights switch it on and off (the
/// editor's play button and a controller's double-click both send a use). The client keeps one
/// music player per room, so the jukebox packets name no item: the room hands them to its first
/// jukebox (<c>RoomActionModule.InteractWithMusicPlayerAsync</c>).
/// </para>
/// </summary>
[RoomObjectLogic("jukebox")]
public class FurnitureJukeboxLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private ImmutableArray<SongDiskSnapshot> _disks;
    private readonly Dictionary<int, SongSnapshot> _songsById = [];

    // The playlist index playing, and room time at which that song started.
    private int _position = -1;
    private long _startedAtMs;

    private IJukeboxGrain Jukebox => _roomGrain._grainFactory.GetJukeboxGrain(_ctx.ObjectId);

    private bool IsPlaying => _position >= 0;

    public override async Task OnAttachAsync(CancellationToken ct)
    {
        await base.OnAttachAsync(ct);

        if (GetState() != JukeboxStates.ON)
            return;

        try
        {
            await PlayFromAsync(0, ct);
        }
        catch (Exception ex)
        {
            // The jukebox still stands; it plays once switched on again.
            _roomGrain._logger.LogError(
                ex,
                "Failed to start jukebox {JukeboxId} in room {RoomId}",
                _ctx.ObjectId,
                _ctx.RoomId
            );
        }
    }

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _position = -1;

        return base.OnPickupAsync(ctx, ct);
    }

    /// <summary>The owner, or anyone with rights here, may switch it on and off.</summary>
    public override async Task<bool> CanUseAsync(ActionContext ctx) =>
        IsItemOwner(ctx) || await HasRightsAsync(ctx);

    /// <summary>
    /// Switches the jukebox off, or on at the playlist index the editor has selected (any other
    /// value, such as a controller's double-click, starts at the top). An empty playlist does
    /// not switch on.
    /// </summary>
    public override async Task OnUseAsync(ActionContext ctx, int param, CancellationToken ct)
    {
        if (GetState() == JukeboxStates.ON)
        {
            await StopAsync(ct);

            return;
        }

        await PlayFromAsync(param, ct);
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        switch (interaction)
        {
            case RequestJukeboxPlaylistInteraction:
                await EnsureLoadedAsync(ct);
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    ComposePlaylist(),
                    ct
                );

                return true;
            case RequestNowPlayingInteraction:
                await EnsureLoadedAsync(ct);
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    ComposeNowPlaying(),
                    ct
                );

                return true;
            case RequestSoundMachinePlaylistInteraction:
                await EnsureLoadedAsync(ct);
                await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                    ctx.PlayerId,
                    ComposeSoundMachinePlaylist(),
                    ct
                );

                return true;
            case AddJukeboxDiskInteraction add:
                if (!IsItemOwner(ctx))
                    return Reject(ctx, interaction, "not the jukebox's owner");

                return await ApplyAsync(
                    ctx,
                    interaction,
                    await Jukebox.AddDiskAsync(ctx.PlayerId, add.DiskId, add.Slot, ct),
                    removedSlot: null,
                    ct
                );
            case RemoveJukeboxDiskInteraction remove:
                if (!IsItemOwner(ctx))
                    return Reject(ctx, interaction, "not the jukebox's owner");

                return await ApplyAsync(
                    ctx,
                    interaction,
                    await Jukebox.RemoveDiskAsync(remove.Slot, ct),
                    removedSlot: remove.Slot,
                    ct
                );
            default:
                return false;
        }
    }

    /// <summary>
    /// Takes the playlist a change left behind and tells the room. The song playing carries on;
    /// when its own disk was taken out, the disk that moved into its place starts instead.
    /// </summary>
    private async Task<bool> ApplyAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        JukeboxChangeResultSnapshot result,
        int? removedSlot,
        CancellationToken ct
    )
    {
        if (result.Result == JukeboxChangeResultType.Full)
        {
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                new JukeboxPlayListFullMessageComposer(),
                ct
            );

            return false;
        }

        if (result.Result != JukeboxChangeResultType.Done)
            return Reject(ctx, interaction, result.Result.ToString());

        var previous = _disks.IsDefault ? [] : _disks;
        var playing = IsPlaying ? previous[_position] : null;

        await SetDisksAsync(result.Disks, ct);
        await _roomGrain.SendComposerToRoomAsync(ComposePlaylist(), ct);

        if (playing is null)
            return true;

        if (_disks.IsEmpty)
        {
            await StopAsync(ct);

            return true;
        }

        if (removedSlot == _position)
        {
            await PlayAsync(_position % _disks.Length, ct);

            return true;
        }

        // The same disk, wherever the change moved it; the clock is unchanged, but what plays
        // next may not be.
        _position = _disks.IndexOf(playing);

        await _roomGrain.SendComposerToRoomAsync(ComposeNowPlaying(), ct);

        return true;
    }

    /// <summary>Switches on at <paramref name="index"/>, or the top when that names no disk.</summary>
    private async Task PlayFromAsync(int index, CancellationToken ct)
    {
        await EnsureLoadedAsync(ct);

        if (_disks.IsEmpty)
        {
            if (GetState() == JukeboxStates.ON)
                await SetStateAsync(JukeboxStates.OFF);

            return;
        }

        if (GetState() != JukeboxStates.ON)
            await SetStateAsync(JukeboxStates.ON);

        await PlayAsync(index >= 0 && index < _disks.Length ? index : 0, ct);
    }

    /// <summary>
    /// Plays the disk at <paramref name="index"/> from its start for the whole room, and the
    /// next when it ends. A disk whose song the hotel no longer has is skipped.
    /// </summary>
    private async Task PlayAsync(int index, CancellationToken ct)
    {
        for (var tried = 0; tried < _disks.Length; tried++)
        {
            var position = (index + tried) % _disks.Length;
            var length = LengthMs(_disks[position]);

            if (length <= 0)
                continue;

            _position = position;
            _startedAtMs = _roomGrain.NowMs();

            TimerSystem.Schedule(
                _ctx.ObjectId,
                length,
                token => PlayAsync((_position + 1) % _disks.Length, token)
            );

            await _roomGrain.SendComposerToRoomAsync(ComposeNowPlaying(), ct);

            return;
        }

        await StopAsync(ct);
    }

    private async Task StopAsync(CancellationToken ct)
    {
        TimerSystem.Cancel(_ctx.ObjectId);
        _position = -1;

        if (GetState() != JukeboxStates.OFF)
            await SetStateAsync(JukeboxStates.OFF);

        await _roomGrain.SendComposerToRoomAsync(NowPlayingMessageComposer.Nothing(), ct);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (!_disks.IsDefault)
            return;

        await SetDisksAsync(await Jukebox.GetDisksAsync(ct), ct);
    }

    /// <summary>Takes a new playlist, fetching the songs it names that are not known yet.</summary>
    private async Task SetDisksAsync(ImmutableArray<SongDiskSnapshot> disks, CancellationToken ct)
    {
        var missing = disks
            .Select(x => x.SongId)
            .Where(songId => !_songsById.ContainsKey(songId))
            .Distinct()
            .ToImmutableArray();

        if (!missing.IsEmpty)
            foreach (
                var song in await _roomGrain
                    ._grainFactory.GetSongDirectoryGrain()
                    .GetSongsAsync(missing, ct)
            )
                _songsById[song.Id] = song;

        _disks = disks;
    }

    private int LengthMs(SongDiskSnapshot disk) =>
        _songsById.TryGetValue(disk.SongId, out var song)
            ? (int)TimeSpan.FromSeconds(song.LengthSeconds).TotalMilliseconds
            : 0;

    private int ElapsedMs() => (int)Math.Max(0, _roomGrain.NowMs() - _startedAtMs);

    private JukeboxSongDisksMessageComposer ComposePlaylist() =>
        new()
        {
            MaxLength = _roomGrain._roomConfig.JukeboxMaxDisks,
            Disks = _disks.IsDefault ? [] : _disks,
        };

    private NowPlayingMessageComposer ComposeNowPlaying()
    {
        if (!IsPlaying || _disks.IsDefaultOrEmpty)
            return NowPlayingMessageComposer.Nothing();

        var next = (_position + 1) % _disks.Length;

        return new()
        {
            CurrentSongId = _disks[_position].SongId,
            CurrentPosition = _position,
            NextSongId = _disks[next].SongId,
            NextPosition = next,
            SyncCountMs = ElapsedMs(),
        };
    }

    /// <summary>
    /// The playlist as a sound machine's client reads it: the songs, and how far into the whole
    /// list the room is, which the client takes modulo the list's length.
    /// </summary>
    private PlayListMessageComposer ComposeSoundMachinePlaylist()
    {
        var disks = _disks.IsDefault ? [] : _disks;
        var before = IsPlaying ? disks.Take(_position).Sum(LengthMs) : 0;

        return new()
        {
            SynchronizationCountMs = IsPlaying ? before + ElapsedMs() : 0,
            Songs =
            [
                .. disks.Select(x => _songsById.GetValueOrDefault(x.SongId)).OfType<SongSnapshot>(),
            ],
        };
    }
}
