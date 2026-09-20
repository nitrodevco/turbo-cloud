using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;

/// <summary>
/// A placeholder addon that puts names into the text of the stack's actions: "$name" becomes
/// the name of the first selected user or furni, or, with param 0 set, of all of them joined by
/// the delimiter after the tab in the string param. Which names is the subclass's.
/// </summary>
public abstract class FurnitureWiredNamePlaceholderLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredTextPlaceholder
{
    private const char SIGIL = '$';

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    /// <summary>The names of what the stack selected, in selection order.</summary>
    protected abstract List<string> GetNames(IWiredSelectionSet selection);

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

        var names = GetNames(ctx.Selected);
        var replacement =
            names.Count == 0 ? string.Empty
            : GetIntParamOrDefault(0, false) ? string.Join(delimiter, names)
            : names[0];

        return Task.FromResult(text.Replace(SIGIL + name, replacement, StringComparison.Ordinal));
    }
}
