using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Moderator;

/// <summary><c>ModToolPreferencesComposer(x, y, width, height)</c>: the issue handler window, sent as it is moved or resized.</summary>
public record ModToolPreferencesMessage : IMessageEvent
{
    public required int WindowX { get; init; }

    public required int WindowY { get; init; }

    public required int WindowWidth { get; init; }

    public required int WindowHeight { get; init; }
}
