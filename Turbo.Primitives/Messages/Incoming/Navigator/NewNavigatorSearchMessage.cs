using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record NewNavigatorSearchMessage : IMessageEvent
{
    public required string SearchCodeOriginal { get; init; }
    public required string FilteringData { get; init; }
}
