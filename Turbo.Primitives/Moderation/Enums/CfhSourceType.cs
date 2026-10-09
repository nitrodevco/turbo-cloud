namespace Turbo.Primitives.Moderation.Enums;

/// <summary>Where a call for help was sent from, which says what its extra columns hold.</summary>
public enum CfhSourceType
{
    /// <summary>The help window, about a player or a room, with room chat lines.</summary>
    Room = 0,

    /// <summary>The messenger's report button, with the conversation's lines.</summary>
    InstantMessage = 1,

    /// <summary>A wall photo's report button: the photo's extra data id and its item.</summary>
    Photo = 2,
}
