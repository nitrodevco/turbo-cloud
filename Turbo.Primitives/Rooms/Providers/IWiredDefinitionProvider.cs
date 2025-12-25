using System;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Primitives.Rooms.Providers;

public interface IWiredDefinitionProvider
{
    public IDisposable RegisterDefinition(
        string wiredType,
        IServiceProvider sp,
        Func<IServiceProvider, IRoomFloorItemContext, IWiredDefinition> factory
    );
    public IWiredDefinition CreateWiredInstance(string wiredType, IRoomFloorItemContext ctx);
}
