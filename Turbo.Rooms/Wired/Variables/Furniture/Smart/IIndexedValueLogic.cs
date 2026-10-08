using System.Threading.Tasks;

namespace Turbo.Rooms.Wired.Variables.Furniture.Smart;

/// <summary>A furni that keeps its settings as int data and lets a smart variable read and set one.</summary>
public interface IIndexedValueLogic
{
    int ValueAt(int index);

    Task SetValueAtAsync(int index, int value);
}
