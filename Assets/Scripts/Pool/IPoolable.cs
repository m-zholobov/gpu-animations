using UnityEngine;

namespace Pool
{
    public interface IPoolable
    {
        GameObject SourcePrefab { get; set; }
    }
}
