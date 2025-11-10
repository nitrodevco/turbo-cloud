using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.NewNavigator;

public sealed record NewNavigatorPreferencesMessage : IComposer
{
    public required int WindowX { get; init; }
    public required int WindowY { get; init; }
    public required int WindowWidth { get; init; }
    public required int WindowHeight { get; init; }
    public required bool LeftPaneHidden { get; init; }
    public required int ResultsMode { get; init; }
}
