using System;

namespace Turbo.Primitives.Gamedata;

/// <summary>The selected definitions no longer match the preview; no repair was applied.</summary>
public sealed class VendingRepairConflictException(string message)
    : InvalidOperationException(message);
