using Microsoft.Extensions.Options;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;

namespace Turbo.Gamedata;

/// <summary><see cref="IGamedataHost"/> from <see cref="GamedataConfig.PublicUrl"/>.</summary>
internal sealed class GamedataHost(IOptionsMonitor<GamedataConfig> config) : IGamedataHost
{
    public string? PublicUrl =>
        string.IsNullOrWhiteSpace(config.CurrentValue.PublicUrl)
            ? null
            : config.CurrentValue.PublicUrl.Trim().TrimEnd('/');
}
