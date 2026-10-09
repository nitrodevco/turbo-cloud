using System;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// A box refusing what its editor sent, with the hotel text that says why
/// (<c>wiredfurni.error.require_click_tiles</c> and its siblings). The room grain turns it into
/// a <see cref="WiredSaveResult"/> for the editor.
/// </summary>
public sealed class WiredSaveRefusedException(string errorKey)
    : Exception($"Wired save refused: {errorKey}")
{
    public string ErrorKey { get; } = errorKey;
}
