using UnityEngine;
using System.Collections.Generic;
using PathInterfaces;

public static class PathTools
{
    public static bool NodeEquatlity(int ax, int ay, int bx, int by)
    {
        return ax == bx && ay == by;
    }

    public class Bounded8Neighbours
    {
        public int lowerX, lowerY, upperX, upperY;
        private static Vector2Int[] NeigborSteps = new Vector2Int[]
        {
            new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1), new Vector2Int(-1,0),
            new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1, -1)
        };

        public Bounded8Neighbours(Vector2Int boundA, Vector2Int boundB)
        {
            lowerX = Mathf.Min(boundA.x, boundB.x);
            lowerY = Mathf.Min(boundA.y, boundB.y);
            upperX = Mathf.Max(boundA.x, boundB.x);
            upperY = Mathf.Max(boundA.y, boundB.y);
        }

        public IEnumerable<Vector2Int> GetNeighbors(int x, int y)
        {
            Vector2Int node = new Vector2Int(x, y);
            List<Vector2Int> neighbors = new List<Vector2Int>(8);
            foreach(Vector2Int step in NeigborSteps)
            {
                Vector2Int neighbor = node + step;
                if (neighbor.x >= lowerX && neighbor.x <= upperX
                    && neighbor.y >= lowerY && neighbor.y <= upperY)
                    neighbors.Add(neighbor);
            }
            return neighbors;
        }

    }

    public class NormalZThresholdWalkable
    {
        public float thresholdPercentile;
        private TerrainData terrainData;
        DGetNeighbors Neighbors;
        public int lowerX, lowerY, upperX, upperY;
        float Resolution;

        public NormalZThresholdWalkable(float percentile, TerrainData terrainData, int Resolution , Vector2Int boundA, Vector2Int boundB)
        {
            lowerX = Mathf.Min(boundA.x, boundB.x);
            lowerY = Mathf.Min(boundA.y, boundB.y);
            upperX = Mathf.Max(boundA.x, boundB.x);
            upperY = Mathf.Max(boundA.y, boundB.y);
            thresholdPercentile = percentile;
            this.Resolution = Resolution;
            this.terrainData = terrainData;
        }

        public bool IsWalkable(int x, int y)
        {
            if (x < lowerX || x > upperX || y < lowerY || y > upperY)
            {
                //Debug.Log(string.Format("{0}, {1} not walkable because it is outside of the grid.", x, y));
                return false;
            }
            return (terrainData.GetInterpolatedNormal(x / Resolution, y / Resolution).y >= thresholdPercentile);
        }
    }

    public class SteepNessThresholdWalkable
    {
        public float thresholdPercentile;
        public float[,] Heights;
        DGetNeighbors Neighbors;
        public int lowerX, lowerY, upperX, upperY;

        public SteepNessThresholdWalkable(float percentile, float[,] heights, DGetNeighbors neighbors, Vector2Int boundA, Vector2Int boundB)
        {
            lowerX = Mathf.Min(boundA.x, boundB.x);
            lowerY = Mathf.Min(boundA.y, boundB.y);
            upperX = Mathf.Max(boundA.x, boundB.x);
            upperY = Mathf.Max(boundA.y, boundB.y);
            Neighbors = neighbors;
            Heights = heights;
            thresholdPercentile = percentile;
            //Debug.Log(string.Format("lx{0} ly{1} ux{2} ux{3}", lowerX, lowerY, upperX, upperY));
        }

        public bool IsWalkable(int x, int y)
        {
            if (x < lowerX || x > upperX || y < lowerY || y > upperY)
            {
                //Debug.Log(string.Format("{0}, {1} not walkable because it is outside of the grid.", x, y));
                return false;
            }
            foreach (Vector2Int neighbor in Neighbors(x, y))
            {
                if ((Heights[x, y] - Heights[neighbor.x, neighbor.y])> thresholdPercentile)
                {
                    //Debug.Log(string.Format("{0}, {1} not walkable because its neighbour ({3}, {4}) has a too big height diff {5} > {6}", x, y, neighbor.x, neighbor.y, Heights[x, y], - Heights[neighbor.x, neighbor.y], thresholdPercentile));
                    return false;
                }
            }
            return true;
        }
    }

    public class CachedWalkable
    {
        private DIsWalkable Walkable;
        public bool[,] WalkableCache;
        public int lowerX, lowerY, upperX, upperY;
        public int Resolution;

        public CachedWalkable(DIsWalkable walkable, Vector2Int boundA, Vector2Int boundB, int Resolution)
        {
            this.Resolution = Resolution;
            Walkable = walkable;
            WalkableCache = new bool[Resolution, Resolution];
            lowerX = Mathf.Min(boundA.x, boundB.x);
            lowerY = Mathf.Min(boundA.y, boundB.y);
            upperX = Mathf.Max(boundA.x, boundB.x);
            upperY = Mathf.Max(boundA.y, boundB.y);
            for (int x = lowerX; x <= upperX; x++)
            {
                for (int y = lowerY; y <= upperY; y++)
                {
                    WalkableCache[x, y] = Walkable(x, y);
                }
            }
        }

        public bool IsWalkable(int x, int y)
        {
            if (x < lowerX || x > upperX || y < lowerY || y > upperY)
            {
                //Debug.Log(string.Format("{0}, {1} not walkable because it is outside of the grid.", x, y));
                return false;
            }
            return WalkableCache[x, y];
        }
    }

    public static bool AlwaysWalkable(int x, int y)
    {
        return true;
    }

    public class Octile8GridHeightStepCost
    {
        private readonly int Steps, Weight;
        public readonly float[,] Heights;

        public Octile8GridHeightStepCost(int steps, int weight, float[,] heights)
        {
            Steps = steps;
            Heights = heights;
            Weight = weight;
        }

        public float StepCosts(int ax, int ay, int bx, int by)
        {
            float hdiff = Heights[ax, ay] - Heights[bx, by];
            hdiff = hdiff > 0 ? hdiff : -hdiff;
            int cost = 1 + ((int) (hdiff * Steps));
            if ((ax - bx != 0) && (ay -by != 0))
            {
                return 1.41f * cost * Weight;
            }
            return cost * Weight;
        }
    }

    public class Octile8GridSlopeStepCost
    {
        private readonly int Steps, Weight;
        public readonly float[,] Heights;

        public Octile8GridSlopeStepCost(int steps, int weight, float[,] heights)
        {
            Steps = steps * steps;
            Heights = heights;
            Weight = weight;
        }

        public float StepCosts(int ax, int ay, int bx, int by)
        {
            float slope = (Heights[ax, ay] - Heights[bx, by])* (Heights[ax, ay] - Heights[bx, by]);
            int cost = 1 + ((int)(slope * Steps));
            if ((ax - bx != 0) && (ay - by != 0))
            {
                return 1.41f * cost * Weight;
            }
            return cost * Weight;
        }
    }
}