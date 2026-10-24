using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
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

    /// <summary>
    /// The name, and the delimiter with "show all", are refused as Habbo refuses them
    /// (<see cref="WiredNameRules.PlaceholderError"/>), and so is a name another placeholder of
    /// this kind on the stack has (<see cref="WiredPlaceholderText.IsNameTakenOnStack"/>).
    /// </summary>
    public override Task<bool> ApplyWiredUpdateAsync(
        ActionContext ctx,
        UpdateWiredMessage update,
        CancellationToken ct
    ) =>
        WiredNameRules.PlaceholderError(update.StringParam, showsAll: update.IntParams is [1, ..])
            is { } error
            ? throw new WiredSaveRefusedException(error)
        : WiredPlaceholderText.IsNameTakenOnStack(
            this,
            FurniModule.GetFloorItemsOnTile(_ctx.GetTileIdx()),
            update.StringParam
        )
            ? throw new WiredSaveRefusedException(WiredSaveErrors.CAPTURE_DESTINATION_CONFLICT)
        : base.ApplyWiredUpdateAsync(ctx, update, ct);

    /// <summary>The names of what the add-on's source selects, in selection order.</summary>
    protected abstract List<string> GetNames(IWiredExecutionContext ctx);

    public override Task<bool> MutatePolicyAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        ctx.Policy.TextPlaceholders.Add(this);

        return Task.FromResult(true);
    }

    public string Token =>
        WiredPlaceholderText.SplitNameAndDelimiter(_wiredData?.StringParam).Item1
            is { Length: > 0 } name
            ? WiredPlaceholderText.Token(SIGIL, name)
            : string.Empty;

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
