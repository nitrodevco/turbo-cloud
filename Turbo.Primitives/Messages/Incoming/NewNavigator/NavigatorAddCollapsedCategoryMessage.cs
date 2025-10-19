using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorAddCollapsedCategoryMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
}
