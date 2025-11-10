using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public sealed record ConvertedRoomIdMessageComposer : IComposer
{
    public required string GlobalId { get; init; }
    public required int ConvertedId { get; init; }
}
