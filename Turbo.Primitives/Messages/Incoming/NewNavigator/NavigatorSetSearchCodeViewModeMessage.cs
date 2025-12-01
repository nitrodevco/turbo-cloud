using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.NewNavigator;

public record NavigatorSetSearchCodeViewModeMessage : IMessageEvent
{
    public string? CategoryName { get; init; }
    public NavigatorViewModeType ViewMode { get; init; }
}
