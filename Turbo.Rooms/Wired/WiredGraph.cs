using System;
using System.Collections.Generic;

namespace Turbo.Rooms.Wired;

internal sealed class WiredGraph
{
    public Dictionary<Type, List<WiredNode>> NodesByEventType { get; } = [];
}
