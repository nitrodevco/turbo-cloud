using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record ConvertedRoomIdMessageComposer : IComposer
{
    public required string GlobalId { get; init; }
    public required int ConvertedId { get; init; }
}
