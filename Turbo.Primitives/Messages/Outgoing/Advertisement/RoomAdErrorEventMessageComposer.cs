using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Advertisement;

/// <summary>
/// An event edit refused for a filtered word: the event window marks the field and puts the
/// filtered text in it (RoomEventViewCtrl.onRoomAdError).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomAdErrorEventMessageComposer : IComposer
{
    /// <summary>Which field held the word: <see cref="NAME"/> or <see cref="DESCRIPTION"/>.</summary>
    [Id(0)]
    public required int ErrorCode { get; init; }

    /// <summary>The field's text with the word replaced.</summary>
    [Id(1)]
    public required string FilteredText { get; init; }

    public const int NAME = 0;
    public const int DESCRIPTION = 1;
}
