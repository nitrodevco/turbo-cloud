using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Primitives.Furniture.Providers;

public interface IStuffDataFactory
{
    public IStuffData CreateStuffData(int typeAndFlags);
    public IStuffData CreateStuffDataFromJson(int typeAndFlags, string jsonString);
}
