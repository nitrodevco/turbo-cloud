using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Chat;

[GenerateSerializer, Immutable]
public record ChatMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required string Text { get; init; }

    [Id(2)]
    public required AvatarGestureType Gesture { get; init; }

    [Id(3)]
    public required int StyleId { get; init; }

    [Id(4)]
    public required List<(string, string, bool)> Links { get; init; }

    [Id(5)]
    public required int TrackingId { get; init; }

    [Id(6)]
    public int? ReceiverRoomIndex { get; init; }

    [Id(7)]
    public int? ChatBubbleWidthOverride { get; init; }

    /// <summary>The bubble shows no "name: " before the text (a bot box with "hide bot name").</summary>
    [Id(8)]
    public bool HideUsername { get; init; }

    /// <summary>
    /// The tile the bubble rises from instead of the speaker's: a WIRED Link Speaker's furni
    /// (WIN63-202610201032 <c>ChatMessageParser</c> reads it last, as two ints).
    /// </summary>
    [Id(9)]
    public ChatBubbleLocation? Location { get; init; }
}

/// <summary>A tile a chat bubble is drawn at.</summary>
[GenerateSerializer, Immutable]
public readonly record struct ChatBubbleLocation([property: Id(0)] int X, [property: Id(1)] int Y);
