using UnityEngine;
using MapToolsInterfaces;
using System.Collections.Generic;

public class OctileDistKernel: IKernel
{
    public float ApplyKernel(Vector2Int self, IEnumerable<Vector2Int> nodes, float[,] values)
    {
        float avg = 0f;
        float normalizer = 0f;
        foreach (Vector2Int node in nodes)
        {
            float dist = MapTools.OctileDistance(self, node);
            avg += values[node.x, node.y] / (1f + dist);
            normalizer += 1f / (1f + dist);
        }
        return avg / normalizer;
    }
}

public class AvgKernel: IKernel
{
    public float ApplyKernel(Vector2Int self, IEnumerable<Vector2Int> nodes, float[,] values)
    {
        float avg = 0f;
        float normalizer = 0f;
        foreach (Vector2Int node in nodes)
        {
            avg += values[node.x, node.y];
            normalizer += 1f;
        }
        return avg / normalizer;
    }
}


