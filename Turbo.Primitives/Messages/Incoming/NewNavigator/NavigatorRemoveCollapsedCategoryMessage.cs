using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorRemoveCollapsedCategoryMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
}
