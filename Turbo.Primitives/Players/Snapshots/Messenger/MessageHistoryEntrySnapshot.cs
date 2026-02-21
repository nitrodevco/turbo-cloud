using System;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Messenger;

[GenerateSerializer, Immutable]
public record MessageHistoryEntrySnapshot
{
    [Id(0)]
    public required PlayerId SenderId { get; init; }

    [Id(1)]
    public required string SenderName { get; init; } = string.Empty;

    [Id(2)]
    public required string SenderFigure { get; init; } = string.Empty;

    [Id(3)]
    public required string Message { get; init; } = string.Empty;

    [Id(4)]
    public required string MessageId { get; init; }

    [Id(5)]
    public required DateTime SentAtUtc { get; init; }
}
