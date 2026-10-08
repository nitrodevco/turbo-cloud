using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Sound;

/// <summary>
/// What the room's jukebox plays. Every field is -1 while it plays nothing; the client stops
/// the song it was playing then.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record NowPlayingMessageComposer : IComposer
{
    public const int NOTHING = -1;

    [Id(0)]
    public required int CurrentSongId { get; init; }

    /// <summary>The playing disk's index in the playlist.</summary>
    [Id(1)]
    public required int CurrentPosition { get; init; }

    /// <summary>The song after it, whose info the client fetches ahead.</summary>
    [Id(2)]
    public required int NextSongId { get; init; }

    [Id(3)]
    public required int NextPosition { get; init; }

    /// <summary>Milliseconds into the playing song, so a player arriving late joins it there.</summary>
    [Id(4)]
    public required int SyncCountMs { get; init; }

    public static NowPlayingMessageComposer Nothing() =>
        new()
        {
            CurrentSongId = NOTHING,
            CurrentPosition = NOTHING,
            NextSongId = NOTHING,
            NextPosition = NOTHING,
            SyncCountMs = NOTHING,
        };
}
