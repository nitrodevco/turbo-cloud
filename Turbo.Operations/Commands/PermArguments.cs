using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

[CommandBranch("check", typeof(PermCheckArguments))]
[CommandBranch("user", typeof(PermUserArguments), Permission = PermissionNodes.Permissions.MANAGE)]
public abstract record PermArguments(PlayerTarget Who, string Node);
