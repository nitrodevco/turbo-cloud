using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetNewNavigatorWindowPreferencesMessage : IMessageEvent
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool OpenSavedSearches { get; init; }
    public NavigatorViewModeType ResultsMode { get; init; }
}
