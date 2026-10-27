namespace Turbo.Contracts.Rooms;

/// <summary>
/// How a line a plugin says over a user is shown (<see cref="IRoomTurn.SayAsync"/>): a plain bubble
/// the whole room sees by default. It is filtered like anything players read, but it is not chat:
/// no flood check, no commands, no chat log.
/// </summary>
public readonly record struct RoomSpeech
{
    /// <summary>Shouted: the bold bubble.</summary>
    public bool Shout { get; init; }

    /// <summary>
    /// A whisper over a player that only that player sees (a word to them alone). Only a player
    /// can be whispered over.
    /// </summary>
    public bool OnlyForThem { get; init; }

    /// <summary>The chat bubble style; 0 is the client's default bubble.</summary>
    public int StyleId { get; init; }

    /// <summary>The bubble's width (the client's -1 to 2); null leaves it to the client.</summary>
    public int? BubbleWidth { get; init; }

    /// <summary>A plain line the whole room sees.</summary>
    public static RoomSpeech Talk => default;

    /// <summary>A whisper only the player it is over sees.</summary>
    public static RoomSpeech Whisper => new() { OnlyForThem = true };
}
