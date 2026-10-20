using System;

namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// The kinds of variable add-on a share or import add-on passes between rooms: the "Categories"
/// checkboxes of <c>wf_xtra_var_share</c> / <c>wf_xtra_var_import</c>, bit n for int param n
/// (AS3 <c>addons/_-226.buildInputs</c>, WIN63-202610201032).
/// </summary>
[Flags]
public enum WiredVariableAddonCategories
{
    None = 0,
    TextConnectors = 1 << 0,
    SubVariableCreators = 1 << 1,
    VariableFx = 1 << 2,
}
