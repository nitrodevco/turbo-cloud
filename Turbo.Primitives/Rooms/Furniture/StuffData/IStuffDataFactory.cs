namespace Turbo.Primitives.Rooms.Furniture.StuffData;

public interface IStuffDataFactory
{
    public IStuffData CreateStuffData(int typeAndFlags);
    public IStuffData CreateStuffDataFromJson(int typeAndFlags, string jsonString);
}
