using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Operations.Commands;

[CommandBranch("add", typeof(GroupAddArguments), Permission = PermissionNodes.Permissions.MANAGE)]
[CommandBranch(
    "remove",
    typeof(GroupRemoveArguments),
    Permission = PermissionNodes.Permissions.MANAGE
)]
public abstract record GroupArguments(PlayerTarget Who, string Group);
