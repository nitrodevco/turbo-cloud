using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

public sealed record HeightMapMessageComposer : IComposer
{
    public required int Width { get; init; }
    public required int Size { get; init; }
    public required short[] Heights { get; init; }
}
