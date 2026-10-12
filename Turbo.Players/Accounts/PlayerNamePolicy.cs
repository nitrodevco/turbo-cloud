using Microsoft.Extensions.Options;
using Turbo.Players.Configuration;
using Turbo.Primitives.Players;

namespace Turbo.Players.Accounts;

/// <summary><see cref="IPlayerNamePolicy"/> from <see cref="PlayerConfig.BlockedNames"/>.</summary>
public sealed class PlayerNamePolicy(IOptions<PlayerConfig> config) : IPlayerNamePolicy
{
    public bool IsBlocked(string name) => PlayerNames.IsBlocked(name, config.Value.BlockedNames);
}
