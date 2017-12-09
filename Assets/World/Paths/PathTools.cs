using UnityEngine;
using System.Collections.Generic;
using PathInterfaces;

public static class PathTools
{
    public static bool NodeEquatlity(int ax, int ay, int bx, int by)
    {
        return ax == bx && ay == by;
    }

    public class Bounded8Neighbours: IGetNeighbors
    {
        public int lowerX, lowerY, upperX, upperY;
        private static Vector2Int[] NeigborSteps = new Vector2Int[]
        {
            new Vector2Int(1,0), new Vector2Int(1,1), new Vector2Int(0,1), new Vector2Int(-1,1), new Vector2Int(-1,0),
            new Vector2Int(-1,-1), new Vector2Int(0,-1), new Vector2Int(1, -1)
        };

        public Bounded8Neighbours(ref Vector2Int boundA, ref Vector2Int boundB)
        {
            lowerX = Mathf.Min(boundA.x, boundB.x);
            lowerY = Mathf.Min(boundA.y, boundB.y);
            upperX = Mathf.Max(boundA.x, boundB.x);
            upperY = Mathf.Max(boundA.y, boundB.y);
        }

        public void GetNeighbors(int x, int y, ref Location2D[] Neighbors)
        {
            for (int i = 0; i < NeigborSteps.Length; i++)
            {
                Neighbors[i].x = NeigborSteps[i].x + x;
                Neighbors[i].y = NeigborSteps[i].y + y;
                Neighbors[i].valid = (Neighbors[i].x >= lowerX && Neighbors[i].x <= upperX
                    && Neighbors[i].y >= lowerY && Neighbors[i].y <= upperY);
            }
        }

        public Location2D[] AllocateArray()
        {
            return new Location2D[8];
        }
    }

    public class NormalYThresholdWalkable
    {
        public float thresholdPercentile;
        private TerrainData terrainData;
        public int lowerX, lowerY, upperX, upperY;
        float Resolution;

        public NormalYThresholdWalkable(float percentile, TerrainData terrainData, int Resolution , ref Vector2Int boundA, ref Vector2Int boundB)
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
        public int lowerX, lowerY, upperX, upperY;
        IGetNeighbors NeighborSource;
        private Location2D[] Neighbours;

        public SteepNessThresholdWalkable(float percentile, float[,] heights, IGetNeighbors neighbors, ref Vector2Int boundA, ref Vector2Int boundB)
        {
            lowerX = Mathf.Min(boundA.x, boundB.x);
            lowerY = Mathf.Min(boundA.y, boundB.y);
            upperX = Mathf.Max(boundA.x, boundB.x);
            upperY = Mathf.Max(boundA.y, boundB.y);
            NeighborSource = neighbors;
            Heights = heights;
            thresholdPercentile = percentile;
            Neighbours = NeighborSource.AllocateArray(); 
            //Debug.Log(string.Format("lx{0} ly{1} ux{2} ux{3}", lowerX, lowerY, upperX, upperY));
        }

        public bool IsWalkable(int x, int y)
        {
            if (x < lowerX || x > upperX || y < lowerY || y > upperY)
            {
                //Debug.Log(string.Format("{0}, {1} not walkable because it is outside of the grid.", x, y));
                return false;
            }
            NeighborSource.GetNeighbors(x, y, ref Neighbours);
            foreach (Location2D neighbor in Neighbours)
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

    public class ConnectivityLabel
    {
        private readonly TerrainData Data;
        private DIsWalkable IsWalkable;
        public int[,] Labels;
        private int nextLabel;
        private readonly int lowerX, lowerY, upperX;

        public ConnectivityLabel(TerrainData data, IGetNeighbors neighborSource, DIsWalkable isWalkable)
        {
            Data = data;
            Labels = new int[data.heightmapResolution, data.heightmapResolution];
            IsWalkable = isWalkable;
            lowerX = 0;
            lowerY = 0;
            upperX = data.heightmapResolution -1;
            CalculateLabels();
        }

        public void CalculateLabels()
        {
            int labelCounter = 1;
            int[] Predecessors = new int[4];
            int validPredecessors;
            List<UnionFindNode<int>> UnionFindTree = new List<UnionFindNode<int>>();
            for (int x = 0; x < Data.heightmapResolution; x++)
            {
                for (int y = 0; y < Data.heightmapResolution; y++)
                {
                    if (!IsWalkable(x, y))
                    {
                        Labels[x, y] = -1;
                        continue;
                    }
                    validPredecessors = 0;
                    if (x - 1 >= lowerX)
                    {
                        if (Labels[x - 1, y] > 0) Predecessors[validPredecessors++] = Labels[x - 1, y];
                        if (y - 1 >= lowerY)
                        {
                            if (Labels[x - 1, y - 1] > 0) Predecessors[validPredecessors++] = Labels[x - 1, y - 1];
                        }
                    }
                    if (y - 1 >= lowerY)
                    {
                        if (Labels[x, y - 1] > 0) Predecessors[validPredecessors++] = Labels[x, y - 1];
                        if (x + 1 <= upperX)
                        {
                            if (Labels[x + 1, y - 1] > 0) Predecessors[validPredecessors++] = Labels[x + 1, y - 1];
                        }
                    }
                    // Cases
                    switch (validPredecessors)
                    {
                        case 0:
                            UnionFindTree.Add(new UnionFindNode<int>(labelCounter));
                            Labels[x, y] = labelCounter;
                            labelCounter++;
                            break;
                        case 1:
                            Labels[x, y] = Predecessors[0];
                            break;
                        case 2:
                            Labels[x, y] = Predecessors[0];
                            if (Predecessors[0] != Predecessors[1])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[0] - 1], UnionFindTree[Predecessors[1] - 1]);
                            }
                            break;
                        case 3:
                            Labels[x, y] = Predecessors[0];
                            if (Predecessors[0] != Predecessors[1])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[0] - 1], UnionFindTree[Predecessors[1] - 1]);
                            }
                            if (Predecessors[0] != Predecessors[2])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[0] - 1], UnionFindTree[Predecessors[2] - 1]);
                            }
                            if (Predecessors[1] != Predecessors[2])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[1] - 1], UnionFindTree[Predecessors[2] - 1]);
                            }
                            break;
                        case 4:
                            Labels[x, y] = Predecessors[0];
                            if (Predecessors[0] != Predecessors[1])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[0] - 1], UnionFindTree[Predecessors[1] - 1]);
                            }
                            if (Predecessors[0] != Predecessors[2])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[0] - 1], UnionFindTree[Predecessors[2] - 1]);
                            }
                            if (Predecessors[1] != Predecessors[2])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[1] - 1], UnionFindTree[Predecessors[2] - 1]);
                            }
                            if (Predecessors[0] != Predecessors[3])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[0] - 1], UnionFindTree[Predecessors[3] - 1]);
                            }
                            if (Predecessors[1] != Predecessors[3])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[1] - 1], UnionFindTree[Predecessors[3] - 1]);
                            }
                            if (Predecessors[2] != Predecessors[3])
                            {
                                UnionFindNode<int>.Union(UnionFindTree[Predecessors[2] - 1], UnionFindTree[Predecessors[3] - 1]);
                            }
                            break;
                    }
                }
            }
            for (int i = 0; i < UnionFindTree.Count; i++)
            {
                UnionFindTree[i] = UnionFindTree[i].Find();
            }
            for (int x = 0; x < Data.heightmapResolution; x++)
            {
                for (int y = 0; y < Data.heightmapResolution; y++)
                {
                    if (Labels[x, y] > 0)
                    {
                        Labels[x, y] = UnionFindTree[Labels[x, y] - 1].Value;
                    }
                }
            }
        }
    }
}