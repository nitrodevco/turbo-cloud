using System;
using Orleans;
using Orleans.Serialization;

namespace Turbo.Contracts.Players;

[GenerateSerializer, Immutable]
public sealed partial class PlayerEvent
{
    [Id(0)] public required string Kind { get; init; }
    [Id(1)] public byte[] Payload { get; init; } = [];
    [Id(2)] public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static PlayerEvent Activated() => new() { Kind = "Activated" };

    public static PlayerEvent Chat(byte[] p) => new() { Kind = "Chat", Payload = p };

}