using UnityEngine;
using System.Collections.Generic;

public class TerrainChunkEdge
{
    public List<int> RoadPoints;
    private Vector2Int ChunkPos1;
    private Vector2Int ChunkPos2;
    private readonly long WorldSeed;
    private readonly int ChunkResolution;

    public override int GetHashCode()
    {
        int hash = 0;

        // to make Edged invariant in order of the inputs sort them here.
        if (ChunkPos1.x > ChunkPos2.x)
        {
            Vector2Int Swap = new Vector2Int(ChunkPos1.x, ChunkPos1.y);
            ChunkPos1 = ChunkPos2;
            ChunkPos2 = Swap;
        }
        if (ChunkPos1.x == ChunkPos2.x && ChunkPos1.y < ChunkPos2.y)
        {
            int yswap = ChunkPos1.y;
            ChunkPos1.y = ChunkPos2.y;
            ChunkPos2.y = yswap;
        }
        // create hash that is withh high probability unique
        unchecked // overflow is not a problem.
        {
            // the multiplicative factors need to be prime.
            hash = ChunkPos1.x;
            hash = ChunkPos1.y + hash * 881;
            hash = 2719 * hash + ChunkPos2.x;
            hash = hash * 2729 + ChunkPos2.y;
            return hash * 3307 + (int)WorldSeed;
        }
    }

    public TerrainChunkEdge(Vector2Int ChunkPos1, Vector2Int ChunkPos2, long WorldSeed, int ChunkResolution)
    {
        this.ChunkPos1 = ChunkPos1;
        this.ChunkPos2 = ChunkPos2;
        this.ChunkResolution = ChunkResolution;
        this.WorldSeed = WorldSeed;
    }

    // Generate points on this edge. Uses the edges hash as seed. IF points are closer to each other then the value indicated by Delta these points will be merged.
    public void GenerateRoadPoints(int MaxRoads = 5, int Delta = 10)
    { 
        System.Random prng = new System.Random(this.GetHashCode());
        int numRoads = prng.Next(1, MaxRoads);
        RoadPoints = new List<int>(numRoads);
        int i = 0;
        while (i < numRoads)
        {
            int candidate = prng.Next(1, ChunkResolution - 2); // Only allow points that are not on the corners of the terrain.
            if (RoadPoints.Contains(candidate)) continue;
            RoadPoints.Add(candidate);
            i++;
        }

        // Merge Points that are close to each other
        RoadPoints.Sort();
        i = 1;
        while( i < RoadPoints.Count)
        {
            if (RoadPoints[i] <= RoadPoints[i - 1] + Delta)
            {
                RoadPoints[i] = (RoadPoints[i] + RoadPoints[i - 1]) / 2;
                RoadPoints.RemoveAt(i - 1);
            }
            else i++;
        }
    }
}
