namespace Turbo.Primitives.Gamedata.Enums;

/// <summary>What a record of the figure data is: one of the three things Habbo's file lists.</summary>
public enum FigureRecordKind
{
    /// <summary>A colour of a palette (<c>&lt;color&gt;</c>), keyed <c>palette/id</c>.</summary>
    Color = 0,

    /// <summary>A kind of clothing (<c>&lt;settype&gt;</c>, <c>hr</c>, <c>ch</c>...), keyed by its type.</summary>
    SetType = 1,

    /// <summary>A piece of clothing (<c>&lt;set&gt;</c>), keyed by its id.</summary>
    Set = 2,
}
