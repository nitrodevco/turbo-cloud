using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Contracts.Wired;

/// <summary>
/// Names a plugin's wired box: the furniture whose logic is <paramref name="key"/> in the hotel's
/// furniture definitions is this box. Found as the plugin loads; the furniture goes back to its old
/// logic when it unloads. A key core already has is replaced, and the inspector says so.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class WiredBoxAttribute(string key) : Attribute
{
    public string Key { get; } = key;
}

/// <summary>
/// What every plugin wired box declares for its editor: the int params it keeps, its text, and
/// whether it acts on users or furni (which sources the editor offers). The client has no editor
/// of a plugin's own, so each kind names the official editor it opens (<c>Editor</c>), and the box
/// declares the inputs that editor sends. Everything a client saves is checked against this before
/// it is stored, exactly as for core's boxes.
/// <para>
/// Derive from <see cref="WiredEffect"/>, <see cref="WiredTrigger{TEvent}"/>,
/// <see cref="WiredCondition"/> or <see cref="WiredSelector"/>. One instance is made per furni, by
/// the plugin's container (take <c>IHotel</c> or the plugin's own services by constructor).
/// </para>
/// </summary>
public abstract class WiredBox
{
    private protected WiredBox() { }

    /// <summary>The int params, in the order the editor sends them; each refuses what it does not allow.</summary>
    public virtual IReadOnlyList<WiredParam> Params => [];

    /// <summary>The longest text a save keeps; null for the hotel's limit for wired text.</summary>
    public virtual int? TextMaxLength => null;

    /// <summary>
    /// Whether the box acts on users: the editor then offers the user sources (the triggering user,
    /// a selector's users, a signal's) and <see cref="IWiredRunContext.Users"/> holds them.
    /// </summary>
    public virtual bool ActsOnUsers => false;

    /// <summary>
    /// Whether the box acts on furni: the editor then lets the player pick furni and offers the furni
    /// sources, and <see cref="IWiredRunContext.Furni"/> holds them.
    /// </summary>
    public virtual bool ActsOnFurni => false;

    /// <summary>How many furni the box may pick; null for the hotel's limit (never more than it).</summary>
    public virtual int? FurniLimit => null;

    /// <summary>The official editor code the client opens for this box, of the box's kind.</summary>
    public abstract int EditorCode { get; }
}

/// <summary>
/// A wired effect: runs when its stack's conditions pass (after the delay the player set), in the
/// stack's order, and flashes as core's effects do. Return whether it did anything.
/// </summary>
public abstract class WiredEffect : WiredBox
{
    /// <summary>
    /// The official effect editor the client opens. Pick one whose inputs are the ones the box
    /// needs: <see cref="WiredActionType.CHAT"/> (Show Message) has a text, for example.
    /// </summary>
    public abstract WiredActionType Editor { get; }

    public sealed override int EditorCode => (int)Editor;

    public abstract Task<bool> ExecuteAsync(IWiredEffectContext ctx, CancellationToken ct);
}

/// <summary>
/// A wired condition: states the positive rule only. The room runs it in the stack's turn, so it
/// is synchronous and reads only what the context gives it.
/// </summary>
public abstract class WiredCondition : WiredBox
{
    /// <summary>The official condition editor the client opens.</summary>
    public abstract WiredConditionType Editor { get; }

    public sealed override int EditorCode => (int)Editor;

    public abstract bool Evaluate(IWiredRunContext ctx);
}

/// <summary>
/// A wired selector: picks the users or furni the stack's effects act on. The filter and invert
/// switches of the editor are applied by the room, as for core's selectors.
/// </summary>
public abstract class WiredSelector : WiredBox
{
    /// <summary>The official selector editor the client opens.</summary>
    public abstract WiredSelectorType Editor { get; }

    public sealed override int EditorCode => (int)Editor;

    /// <summary>Whether it picks users (true) or furni (false).</summary>
    public virtual bool SelectsUsers => false;

    public abstract WiredSelection Select(IWiredRunContext ctx);
}

/// <summary>What a selector picked: furni by id, users by room index.</summary>
public sealed record WiredSelection(IReadOnlyCollection<int> Furni, IReadOnlyCollection<int> Users)
{
    public static WiredSelection OfFurni(IEnumerable<int> furni) => new([.. furni], []);

    public static WiredSelection OfUsers(IEnumerable<int> users) => new([], [.. users]);
}

/// <summary>
/// One int param of a box, as its editor sends it: a value the rule does not allow refuses the
/// save, and a stored one that no longer fits loads as the default.
/// </summary>
public sealed record WiredParam
{
    private WiredParam(int @default, int min, int max, IReadOnlyList<int>? values, bool isBool)
    {
        Default = @default;
        Min = min;
        Max = max;
        Values = values;
        IsBool = isBool;
    }

    public int Default { get; }

    public int Min { get; }

    public int Max { get; }

    /// <summary>The values allowed, when the param is one of a list.</summary>
    public IReadOnlyList<int>? Values { get; }

    /// <summary>A switch: 0 or 1.</summary>
    public bool IsBool { get; }

    /// <summary>A number from <paramref name="min"/> to <paramref name="max"/>.</summary>
    public static WiredParam Range(int min, int max, int @default) =>
        new(@default, min, max, null, isBool: false);

    /// <summary>A switch, 0 or 1.</summary>
    public static WiredParam Switch(bool @default) =>
        new(@default ? 1 : 0, 0, 1, null, isBool: true);

    /// <summary>One of <paramref name="values"/>, as a radio group or a dropdown offers them.</summary>
    public static WiredParam OneOf(int @default, params int[] values) =>
        new(@default, int.MinValue, int.MaxValue, values, isBool: false);
}
