using Turbo.Primitives.Players;

namespace Turbo.Primitives.Commands;

/// <summary>A player a command's target resolved to, with the name as the hotel spells it.</summary>
public readonly record struct ResolvedPlayer(PlayerId Id, string Name);
