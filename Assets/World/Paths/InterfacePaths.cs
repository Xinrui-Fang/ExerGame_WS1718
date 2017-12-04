using System.Collections.Generic;
using UnityEngine;
using UtilsInterface;

namespace PathInterfaces
{
    public delegate bool DIsWalkable(int x, int y);
    public delegate bool DIsGoal(int ax, int ay, int bx, int by);
    public delegate float DGetStepCost(int ax, int ay, int bx, int by);

    public interface IPathSearch
    {
        List<Vector2Int> Search(Vector2Int start, Vector2Int goal);
    }

    public interface IGetNeighbors: IFixedSizeArrayIterator<Location2D>
    {
        void GetNeighbors(int x, int y, Location2D[] NeighborsArray);
    }
}