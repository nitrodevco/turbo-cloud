using Microsoft.Extensions.Options;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Inventory;

namespace Turbo.Inventory;

/// <summary><see cref="IGivableEffects"/> by <see cref="EffectConfig.CanGive"/>, the rule the effect grain grants by.</summary>
public sealed class GivableEffects(IOptions<EffectConfig> config) : IGivableEffects
{
    public bool CanGive(int effectId) => config.Value.CanGive(effectId);
}
