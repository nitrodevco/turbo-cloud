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
/// A placeholder addon that puts names into the text of the stack's actions: "$(name)" becomes
/// the name of the first selected user or furni, or, with param 0 set, of all of them joined by
/// the delimiter after the tab in the string param. Which names is the subclass's, read from the
/// add-on's own source: two placeholders on one stack may name the triggering user and the
/// selector's (Wired Faculty, "How to make bot say the name of both the Triggering and the
/// Target user", 24/05/2025).
/// </summary>
public abstract class FurnitureWiredNamePlaceholderLogic(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredAddonLogic(grainFactory, stuffDataFactory, ctx), IWiredTextPlaceholder
{
    private const char SIGIL = '$';

    public override List<IWiredParamRule> GetIntParamRules() => [new WiredBoolParamRule(false)];

    /// <summary>The names of what the add-on's source selects, in selection order.</summary>
    protected abstract List<string> GetNames(IWiredExecutionContext ctx);

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.TextPlaceholders.Add(this);

        return Task.FromResult(true);
    }

    public Task<string> ApplyAsync(IWiredExecutionContext ctx, string text, CancellationToken ct)
    {
        var (name, delimiter) = WiredPlaceholderText.SplitNameAndDelimiter(_wiredData.StringParam);

        if (
            name.Length == 0
            || !text.Contains(WiredPlaceholderText.Token(SIGIL, name), StringComparison.Ordinal)
        )
            return Task.FromResult(text);

        var names = GetNames(ctx);
        var replacement =
            names.Count == 0 ? string.Empty
            : GetIntParamOrDefault(0, false) ? string.Join(delimiter, names)
            : names[0];

        return Task.FromResult(
            text.Replace(
                WiredPlaceholderText.Token(SIGIL, name),
                replacement,
                StringComparison.Ordinal
            )
        );
    }
}
