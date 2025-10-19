using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record NavigatorRemoveCollapsedCategoryMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
}
