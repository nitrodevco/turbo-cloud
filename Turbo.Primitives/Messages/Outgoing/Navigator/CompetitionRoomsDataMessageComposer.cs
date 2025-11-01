using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Navigator;

public record CompetitionRoomsDataMessageComposer : IComposer
{
    public object? Data { get; init; }
}
