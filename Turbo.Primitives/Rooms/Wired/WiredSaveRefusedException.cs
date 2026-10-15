using System;
using System.Collections.Generic;

namespace Turbo.Primitives.Rooms.Wired;

/// <summary>
/// A box refusing what its editor sent, with the hotel text that says why
/// (<c>wiredfurni.error.require_click_tiles</c> and its siblings). The room grain turns it into
/// a <see cref="WiredSaveResult"/> for the editor. The parameters fill the text's
/// <c>%name%</c> slots (<c>IncomingMessages.onValidationError</c> passes them to
/// <c>getLocalizationWithParamMap</c>, AS3 WIN63-202610131318).
/// </summary>
public sealed class WiredSaveRefusedException(
    string errorKey,
    params (string Key, string Value)[] parameters
) : Exception($"Wired save refused: {errorKey}")
{
    public string ErrorKey { get; } = errorKey;

    public IReadOnlyList<(string Key, string Value)> Parameters { get; } = parameters;
}
