using UnityEngine;
using System.Collections;

public class TerrainChunk
{
    public Vector2Int GridCoords;
    public int ChunkSeed;
    public float[,] Heights;
    public Terrain ChunkTerrain;

    private long WorldSeed;
    public TerrainChunkEdge[] TerrainEdges;
    public GameSettings Settings;
    private TerrainData ChunkTerrainData;
    private VegetationGenerator vGen;
    private PathFinder paths;

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = GridCoords.x;
            hash = GridCoords.y + hash * 881;
            return hash * 2719 + (int) WorldSeed;
        }
    }
    
    public TerrainChunk(GameSettings settings)
    {
        Settings = settings;
        WorldSeed = Settings.WorldSeed;
        ChunkTerrainData = new TerrainData
        {
            heightmapResolution = Settings.HeightmapResolution,
            size = new Vector3(Settings.Size, Settings.Depth, Settings.Size),
            splatPrototypes = Settings.GetSplat(),
            detailPrototypes = Settings.GetDetail()
        };
        ChunkTerrainData.SetDetailResolution(Settings.DetailResolution, Settings.DetailResolutionPerPatch);
        vGen = new VegetationGenerator();
    }

    public void Build(Vector2Int gridCoords)
    {
        // Resetting all the Arrays
        Heights = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
        GridCoords = gridCoords;
        ChunkSeed = GetHashCode();
        TerrainEdges = new TerrainChunkEdge[4]
        {
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(1,0), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(-1,0), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,1), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,-1), WorldSeed, Settings.HeightmapResolution)
        };

        Settings.GetHeightMapGenerator(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Heights, Settings.HeightmapResolution, Settings.Size);
        ChunkTerrainData.SetHeights(0, 0, Heights);

        Vector2Int lowerBound = new Vector2Int(0, 0);
        Vector2Int upperBound = new Vector2Int(Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1);
        PathTools.Bounded8Neighbours neighbours = new PathTools.Bounded8Neighbours(ref lowerBound, ref upperBound);
        PathTools.NormalYThresholdWalkable walkable_src = new PathTools.NormalYThresholdWalkable(
            Mathf.Cos(Mathf.Deg2Rad * 30),
            ChunkTerrainData, 
            Settings.HeightmapResolution, ref lowerBound, ref upperBound);
        PathTools.CachedWalkable walkable = new PathTools.CachedWalkable(walkable_src.IsWalkable, lowerBound, upperBound, Settings.HeightmapResolution);
        PathTools.Octile8GridSlopeStepCost AStarStepCost = new PathTools.Octile8GridSlopeStepCost(5000, 5, Heights);

        PathTools.ConnectivityLabel connectivity = new PathTools.ConnectivityLabel(ChunkTerrainData, neighbours, walkable.IsWalkable);
        paths = new PathFinder(AStarStepCost.StepCosts, Settings.HeightmapResolution, Heights, connectivity);
        AStar search = new AStar(walkable.IsWalkable, neighbours, paths.StepCostsRoad, MapTools.OctileDistance, 5f);
        paths.SetSearch(search);
        search.PrepareSearch(Settings.HeightmapResolution * Settings.HeightmapResolution);
        System.Random prng = new System.Random((int)ChunkSeed);
        Vector2Int start = new Vector2Int();
        Vector2Int end = new Vector2Int();
        for (int i = 0; i < 3; i++)
        {
            start = MapTools.UnfoldToPerimeter(prng.Next(0, 4 * (Settings.HeightmapResolution - 1)), Settings.HeightmapResolution - 1);
            end = MapTools.UnfoldToPerimeter(prng.Next(0, 4 * (Settings.HeightmapResolution - 1)), Settings.HeightmapResolution - 1);
            paths.MakePath(start, end);
        }
        search.CleanUp();
        ChunkTerrainData.SetHeights(0, 0, paths.Heights);

        ChunkTerrainData.RefreshPrototypes();
        ChunkTerrainData = TerrainLabeler.MapTerrain(new OpenSimplexProvider((long) ChunkSeed), ChunkTerrainData, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, gridCoords * Settings.HeightmapResolution);
        vGen.PaintGras(ChunkSeed, Heights, Settings.Trees, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, ChunkTerrainData);
    }

    public void Flush()
    {
        GameObject UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
        Terrain terrain =  UnityTerrain.GetComponent<Terrain>();
        terrain.materialType = Terrain.MaterialType.Custom;
        terrain.materialTemplate = Settings.TerrainMaterial;
        UnityTerrain.SetActive(true);
        UnityTerrain.transform.Translate(new Vector3(GridCoords.y, 0, GridCoords.x) * Settings.Size);
    }
}