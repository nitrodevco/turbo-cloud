using Turbo.Contracts.Abstractions;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Messages.Incoming.Preferences;

public record SetNewNavigatorWindowPreferencesMessage : IMessageEvent
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public bool OpenSavedSearches { get; init; }
    public NavigatorResultsMode ResultsMode { get; init; }
}
