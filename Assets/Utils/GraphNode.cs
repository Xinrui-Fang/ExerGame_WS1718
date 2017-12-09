using System.Collections.Generic;
using UnityEngine;

namespace Assets.Utils
{
    public struct GraphNode
    {
        public int Value;
        public Vector2Int location;
        public List<GraphNode> Neightbors;
    }
}
