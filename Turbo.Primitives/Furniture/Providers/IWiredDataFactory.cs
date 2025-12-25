using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.WiredData;

namespace Turbo.Primitives.Furniture.Providers;

public interface IWiredDataFactory
{
    public IWiredData CreateWiredData(WiredType type);
    public IWiredData CreateWiredDataFromJson(WiredType type, string? jsonData);
    public IWiredData CreateWiredDataFromExtraData(WiredType type, IExtraData extraData);
}
