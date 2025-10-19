using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record NavigatorSetSearchCodeViewModeMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
    public NavigatorResultsMode ViewMode { get; init; }
}
