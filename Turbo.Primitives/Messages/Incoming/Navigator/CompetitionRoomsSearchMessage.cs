using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record CompetitionRoomsSearchMessage : IMessageEvent
{
    public int GoalId { get; init; }
    public int PageIndex { get; init; }
}
