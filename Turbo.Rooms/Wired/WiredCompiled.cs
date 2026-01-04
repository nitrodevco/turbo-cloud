using System;
using System.Collections.Generic;

namespace Turbo.Rooms.Wired;

public sealed class WiredCompiled
{
    public Dictionary<int, WiredStack> StacksById { get; } = [];

    public Dictionary<Type, List<int>> StackIdsByEventType { get; } = [];
}
