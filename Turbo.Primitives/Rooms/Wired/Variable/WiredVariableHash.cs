using Orleans;

namespace Turbo.Primitives.Rooms.Wired.Variable;

[GenerateSerializer, Immutable]
public readonly record struct WiredVariableHash(int Value);
