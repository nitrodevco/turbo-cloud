namespace Turbo.Primitives.Furniture.Enums;

/// <summary>
/// Which furni a recolor applies to, as the recolor window's "Apply to" menu numbers it
/// (<c>furniture.recolor.scope.0-3</c>).
/// </summary>
public enum RecolorScopeType
{
    OnlyThis = 0,
    SameColor = 1,
    SameType = 2,
    SameTypeAndColor = 3,
}
