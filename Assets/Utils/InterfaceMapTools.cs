using UnityEngine;
using System.Collections.Generic;

namespace MapToolsInterfaces
{
    public interface IKernel
    {
        float ApplyKernel(Vector2Int node, IEnumerable<Vector2Int> nodes, float[,] values);
    }
}