using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Furniture.WiredData;

public interface IWiredData
{
    public int WiredCode { get; set; }
    public int FurniLimit { get; set; }
    public List<int> StuffIds { get; set; }
    public List<int> IntParams { get; set; }
    public List<long> VariableIds { get; set; }
    public string StringParam { get; set; }
    public Dictionary<int, WiredSourceType> FurniSources { get; set; }
    public Dictionary<int, WiredSourceType> PlayerSources { get; set; }
    public void SetAction(Func<Task>? onSnapshotChanged);
}
