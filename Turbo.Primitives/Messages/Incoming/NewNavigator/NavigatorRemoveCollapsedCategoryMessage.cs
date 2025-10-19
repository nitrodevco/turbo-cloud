using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorRemoveCollapsedCategoryMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
}
