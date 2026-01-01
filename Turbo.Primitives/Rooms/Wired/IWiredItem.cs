using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Furniture.WiredData;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Wired;

public interface IWiredItem
{
    public int Id { get; }
    public IWiredData WiredData { get; }
    public List<WiredFurniSourceType[]> GetFurniSources();
    public List<WiredPlayerSourceType[]> GetPlayerSources();
    public List<WiredFurniSourceType[]> GetAllowedFurniSources();
    public List<WiredPlayerSourceType[]> GetAllowedPlayerSources();
    public List<WiredFurniSourceType[]> GetDefaultFurniSources();
    public List<WiredPlayerSourceType[]> GetDefaultPlayerSources();
    public List<Type> GetDefinitionSpecificTypes();
    public List<Type> GetTypeSpecificTypes();
    public List<object> GetDefinitionSpecifics();
    public List<object> GetTypeSpecifics();
    public Task FlashActivationStateAsync();
}
