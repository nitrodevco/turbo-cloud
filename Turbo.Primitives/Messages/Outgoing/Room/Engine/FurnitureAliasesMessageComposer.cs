using System.Collections.Generic;
using Turbo.Contracts.Abstractions;

namespace Turbo.Primitives.Messages.Outgoing.Room.Engine;

public sealed record FurnitureAliasesMessageComposer : IComposer
{
    public required List<(string, string)> Aliases { get; init; }
}
