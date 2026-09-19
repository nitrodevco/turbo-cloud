namespace Turbo.Primitives.Furniture.Enums;

/// <summary>
/// What a video display is doing. The client uses the same two numbers for the state it is told
/// with a video and for the command that changes it later (<c>YoutubeDisplayWidget</c>): a video
/// that arrives paused is paused as soon as its player has loaded.
/// </summary>
public enum YoutubeVideoStateType
{
    Playing = 1,
    Paused = 2,
}
