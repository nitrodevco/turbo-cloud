using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Interactions;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor;

/// <summary>
/// A video display. The hotel lists the playlists (<see cref="RoomConfig.YoutubePlaylists"/>);
/// whoever may edit furniture here picks one and works the controls, and everyone in the room
/// watches the same video at the same point. The client plays the video itself: the room only
/// says which one, how far in the room already is, and when to pause, so it keeps a clock, not
/// a stream. The chosen playlist is kept with the item; the position is not, and a display
/// starts its playlist over when the room loads.
/// </summary>
[RoomObjectLogic("youtube")]
public class FurnitureYoutubeLogic(IStuffDataFactory stuffDataFactory, IRoomFloorItemContext ctx)
    : FurnitureFloorLogic(stuffDataFactory, ctx)
{
    private int _videoIndex;
    private bool _isPaused;

    // Room time at which the current video would have started had it never been paused, and
    // how far in it was when it was paused.
    private long _startedAtMs;
    private int _pausedAtSeconds;

    public override Task OnPickupAsync(ActionContext ctx, CancellationToken ct)
    {
        _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);

        return base.OnPickupAsync(ctx, ct);
    }

    public override async Task<bool> OnInteractAsync(
        ActionContext ctx,
        FurnitureInteraction interaction,
        CancellationToken ct
    )
    {
        switch (interaction)
        {
            case RequestYoutubeStatusInteraction:
                await SendStatusAsync(ctx, ct);

                return true;
            case SetYoutubePlaylistInteraction choose:
                if (!await HasRightsAsync(ctx))
                    return Reject(ctx, interaction, "no rights");

                if (FindPlaylist(choose.PlaylistId) is null)
                    return Reject(ctx, interaction, "unknown playlist");

                _ctx.RoomObject.ExtraData.UpdateSection(
                    YoutubeDisplayData.SECTION,
                    new YoutubeDisplayData { PlaylistId = choose.PlaylistId }
                );

                await StartVideoAsync(0, ct);
                await SendStatusAsync(ctx, ct);

                return true;
            case ControlYoutubePlaybackInteraction control:
                if (!await HasRightsAsync(ctx))
                    return Reject(ctx, interaction, "no rights");

                return await ControlAsync(control.Command, ct);
            default:
                return false;
        }
    }

    private async Task<bool> ControlAsync(YoutubePlaybackCommandType command, CancellationToken ct)
    {
        if (GetPlaylist() is not { Videos.Count: > 0 } playlist)
            return false;

        switch (command)
        {
            case YoutubePlaybackCommandType.Next:
                await StartVideoAsync(_videoIndex + 1, ct);

                return true;
            case YoutubePlaybackCommandType.Previous:
                await StartVideoAsync(_videoIndex - 1 + playlist.Videos.Count, ct);

                return true;
            case YoutubePlaybackCommandType.Pause when !_isPaused:
                _pausedAtSeconds = GetPositionSeconds();
                _isPaused = true;

                _roomGrain.TimerSystem.Cancel(_ctx.ObjectId);
                break;
            case YoutubePlaybackCommandType.Play when _isPaused:
                _isPaused = false;
                _startedAtMs =
                    _roomGrain.NowMs()
                    - (long)TimeSpan.FromSeconds(_pausedAtSeconds).TotalMilliseconds;

                ScheduleNext(playlist.Videos[_videoIndex]);
                break;
            default:
                return false;
        }

        await _roomGrain.SendComposerToRoomAsync(
            new YoutubeControlVideoMessageComposer
            {
                FurniId = _ctx.ObjectId,
                State = _isPaused ? YoutubeVideoStateType.Paused : YoutubeVideoStateType.Playing,
            },
            ct
        );

        return true;
    }

    /// <summary>Starts a video of the playlist from its beginning, wrapping around, for the whole room.</summary>
    private async Task StartVideoAsync(int index, CancellationToken ct)
    {
        if (GetPlaylist() is not { Videos.Count: > 0 } playlist)
            return;

        _videoIndex = index % playlist.Videos.Count;
        _isPaused = false;
        _pausedAtSeconds = 0;
        _startedAtMs = _roomGrain.NowMs();

        ScheduleNext(playlist.Videos[_videoIndex]);

        await _roomGrain.SendComposerToRoomAsync(ComposeVideo(), ct);
    }

    // The client does not say when a video ends, so the room moves on when its length is up.
    private void ScheduleNext(YoutubeVideoConfig video)
    {
        var remainingSeconds = Math.Max(1, video.DurationSeconds - GetPositionSeconds());

        _roomGrain.TimerSystem.Schedule(
            _ctx.ObjectId,
            (int)Math.Min(int.MaxValue, TimeSpan.FromSeconds(remainingSeconds).TotalMilliseconds),
            ct => StartVideoAsync(_videoIndex + 1, ct)
        );
    }

    /// <summary>The playlists first, then the video: the client's widget merges the two.</summary>
    private async Task SendStatusAsync(ActionContext ctx, CancellationToken ct)
    {
        // A display nobody has asked about since the room loaded has a playlist and no clock.
        if (_startedAtMs == 0 && GetPlaylist() is { Videos.Count: > 0 })
            await StartVideoAsync(0, ct);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new YoutubeDisplayPlaylistsMessageComposer
            {
                FurniId = _ctx.ObjectId,
                Playlists =
                [
                    .. _roomGrain._roomConfig.YoutubePlaylists.Select(
                        x => new YoutubePlaylistSnapshot
                        {
                            PlaylistId = x.Id,
                            Title = x.Title,
                            Description = x.Description,
                        }
                    ),
                ],
                SelectedPlaylistId = GetPlaylist()?.Id ?? string.Empty,
            },
            ct
        );
        await _roomGrain._grainFactory.SendComposerToPlayerAsync(ctx.PlayerId, ComposeVideo(), ct);
    }

    private YoutubeDisplayVideoMessageComposer ComposeVideo()
    {
        var video = GetPlaylist() is { Videos.Count: > 0 } playlist
            ? playlist.Videos[_videoIndex % playlist.Videos.Count]
            : null;

        return new YoutubeDisplayVideoMessageComposer
        {
            FurniId = _ctx.ObjectId,
            VideoId = video?.VideoId ?? string.Empty,
            StartAtSeconds = video is null ? 0 : GetPositionSeconds(),
            EndAtSeconds = video?.DurationSeconds ?? 0,
            State = _isPaused ? YoutubeVideoStateType.Paused : YoutubeVideoStateType.Playing,
        };
    }

    private int GetPositionSeconds() =>
        _isPaused
            ? _pausedAtSeconds
            : (int)
                Math.Max(
                    0,
                    TimeSpan.FromMilliseconds(_roomGrain.NowMs() - _startedAtMs).TotalSeconds
                );

    private YoutubePlaylistConfig? GetPlaylist()
    {
        var stored = FurnitureExtraDataSections.Read<YoutubeDisplayData>(
            _ctx.RoomObject.ExtraData,
            YoutubeDisplayData.SECTION,
            _roomGrain._logger
        );

        return FindPlaylist(stored?.PlaylistId);
    }

    private YoutubePlaylistConfig? FindPlaylist(string? playlistId) =>
        string.IsNullOrEmpty(playlistId)
            ? null
            : _roomGrain._roomConfig.YoutubePlaylists.FirstOrDefault(x => x.Id == playlistId);
}
