using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Turbo.Primitives.Furniture;

public interface IStorageData
{
    public Dictionary<string, int> Storage { get; set; }
    public bool TryGet(string key, out int value);
    public void SetValue(string key, int value);
    public void Remove(string key);
    public void SetAction(Func<Task>? onChanged);
}
