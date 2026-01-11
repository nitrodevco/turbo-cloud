using System;

namespace Turbo.Rooms.Wired.Variables;

public sealed class WiredVariableRegistration
{
    public required WiredVariableDefinition Definition { get; init; }
    public required Func<string, (bool Success, int Value)> Getter { get; init; }
    public required Action<string, int> Setter { get; init; }
    public required Action<string> Remover { get; init; }
}
