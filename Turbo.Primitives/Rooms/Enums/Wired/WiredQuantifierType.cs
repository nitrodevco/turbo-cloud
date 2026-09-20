namespace Turbo.Primitives.Rooms.Enums.Wired;

/// <summary>
/// What a condition's shared quantifier radio counts, as the client's <c>QuantifierType</c>
/// numbers it. The server declares this per condition box and the client only reads it: it
/// picks the wording of the radio (<c>wiredfurni.params.quantifier.furni|users|variables</c>)
/// and <see cref="None"/> hides the radio altogether. It is never sent back, so it is not
/// stored with the box.
/// </summary>
public enum WiredQuantifierType : byte
{
    None = 0,
    Furni = 1,
    Users = 2,
    Variables = 3,
}
