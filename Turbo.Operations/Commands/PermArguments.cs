using Turbo.Primitives.Commands;

namespace Turbo.Operations.Commands;

[CommandBranch("check", typeof(PermCheckArguments))]
public abstract record PermArguments(PlayerTarget Who, string Node);
