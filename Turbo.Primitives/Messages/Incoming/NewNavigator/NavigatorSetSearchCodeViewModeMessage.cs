using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorSetSearchCodeViewModeMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
    public NavigatorViewModeType ViewMode { get; init; }
}
