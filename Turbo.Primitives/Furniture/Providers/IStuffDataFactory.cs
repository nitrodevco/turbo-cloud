using Turbo.Primitives.Furniture.StuffData;

namespace Turbo.Primitives.Furniture.Providers;

public interface IStuffDataFactory
{
    public IStuffData CreateStuffData(StuffDataType type);
    public IStuffData CreateStuffDataFromJson(StuffDataType type, string? jsonData);
    public IStuffData CreateStuffDataFromExtraData(StuffDataType type, IExtraData extraData);

    /// <summary>
    /// As <see cref="CreateStuffDataFromExtraData(StuffDataType, IExtraData)"/>, but an item with
    /// nothing stored starts with <paramref name="emptyLegacyState"/> as its legacy data instead of
    /// "0": a stateless item's is empty, as Habbo sends it.
    /// </summary>
    public IStuffData CreateStuffDataFromExtraData(
        StuffDataType type,
        IExtraData extraData,
        string emptyLegacyState
    );
}
