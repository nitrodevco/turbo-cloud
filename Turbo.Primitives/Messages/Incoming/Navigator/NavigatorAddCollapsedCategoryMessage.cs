using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record NavigatorAddCollapsedCategoryMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
}
