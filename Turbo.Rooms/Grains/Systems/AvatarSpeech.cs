using Turbo.Primitives.Rooms.Object.Avatars;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// How a line from <see cref="RoomChatSystem.SayAsAvatarAsync"/> is shown. The default is a plain
/// bubble the whole room sees.
/// </summary>
public readonly record struct AvatarSpeech
{
    /// <summary>The chat bubble style; zero is the client's default bubble.</summary>
    public int StyleId { get; init; }

    public bool Shout { get; init; }

    /// <summary>When set, a whisper: only this player sees the bubble.</summary>
    public IRoomPlayer? OnlyFor { get; init; }

    /// <summary>Null leaves the width to the client.</summary>
    public int? BubbleWidth { get; init; }
}
