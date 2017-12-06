using UnityEngine;
using System.Collections;

public class TerrainChunk
{
    public Vector2Int WorldPosition;
    public int ChunkSeed;
    public bool[,] StreetMap;
    public int[,] ConnectivityMap;
    public float[,] Heights;
    public Terrain ChunkTerrain;

    private long WorldHash;

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = WorldPosition.x;
            hash = WorldPosition.y + hash * 881;
            return hash * 2719 + (int) WorldHash;
        }
    }
    
    public TerrainChunk()
    {

    }

}
