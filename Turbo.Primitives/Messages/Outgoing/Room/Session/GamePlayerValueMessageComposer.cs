using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Session;

public record GamePlayerValueMessageComposer : IComposer
{
    public required int UserId { get; init; }
    public required int Value { get; init; }
}
