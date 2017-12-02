using System.Collections.Generic;
using UnityEngine;

namespace PathInterfaces
{
    public delegate bool DIsWalkable(int x, int y);
    public delegate bool DIsGoal(int ax, int ay, int bx, int by);
    public delegate void DGetNeighbors(int x, int y, Location2D[] Neighbors);
    public delegate float DGetStepCost(int ax, int ay, int bx, int by);

    public interface PathSearch
    {
        List<Vector2Int> Search(Vector2Int start, Vector2Int goal);
    }
}