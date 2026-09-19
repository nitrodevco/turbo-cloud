using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// Replaces "$name" in the text of the stack actions with the triggering user name. Param 0
/// shows every selected user, joined by the delimiter after the tab in the string param.
/// </summary>
[RoomObjectLogic("wf_xtra_text_output_username")]
public class WiredAddonUsernamePlaceholder(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredTextPlaceholder
{
    private const char SIGIL = '$';

    public override int WiredCode => (int)WiredAddonType.USERNAME_PLACEHOLDER;

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.TextPlaceholders.Add(this);

        return Task.FromResult(true);
    }

    public Task<string> ApplyAsync(IWiredExecutionContext ctx, string text, CancellationToken ct)
    {
        var (name, delimiter) = WiredPlaceholderText.SplitNameAndDelimiter(_wiredData.StringParam);

        if (name.Length == 0 || !text.Contains(SIGIL + name, StringComparison.Ordinal))
            return Task.FromResult(text);

        var names = GetPlayers(ctx.Selected).Select(x => x.Name).ToList();
        var replacement =
            names.Count == 0 ? string.Empty
            : GetIntParamOrDefault(0, false) ? string.Join(delimiter, names)
            : names[0];

        return Task.FromResult(text.Replace(SIGIL + name, replacement, StringComparison.Ordinal));
    }
}
