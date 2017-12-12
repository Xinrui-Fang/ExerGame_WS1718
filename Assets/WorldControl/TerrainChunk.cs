using Assets.Utils;
using System;
using UnityEngine;

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
    public PathTools.ConnectivityLabel Connectivity;
    GameObject UnityTerrain;

    public bool DEBUG_ON = false;

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
        float[,] Moiisture = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];

        GridCoords = gridCoords;
        ChunkSeed = GetHashCode();
        TerrainEdges = new TerrainChunkEdge[4]
        {
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,-1), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(1,0), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,1), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(-1,0), WorldSeed, Settings.HeightmapResolution)
        };

        Settings.GetHeightMapGenerator(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Heights, Settings.HeightmapResolution, Settings.Size);
        Settings.Moisture.GetHeightSource(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Moiisture, Settings.HeightmapResolution, Settings.Size);
        ChunkTerrainData.SetHeights(0, 0, Heights);

        Vector2Int lowerBound = new Vector2Int(0, 0);
        Vector2Int upperBound = new Vector2Int(Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1);
        PathTools.Bounded8Neighbours neighbours = new PathTools.Bounded8Neighbours(ref lowerBound, ref upperBound);
        PathTools.NormalYThresholdWalkable walkable_src = new PathTools.NormalYThresholdWalkable(
            Mathf.Cos(Mathf.Deg2Rad * 25),
            ChunkTerrainData, 
            Settings.HeightmapResolution, ref lowerBound, ref upperBound);
        PathTools.CachedWalkable walkable = new PathTools.CachedWalkable(walkable_src.IsWalkable, lowerBound, upperBound, Settings.HeightmapResolution);
        PathTools.Octile8GridSlopeStepCost AStarStepCost = new PathTools.Octile8GridSlopeStepCost(5000, 10, Heights);

        Connectivity = new PathTools.ConnectivityLabel(ChunkTerrainData, neighbours, walkable.IsWalkable);
        paths = new PathFinder(AStarStepCost.StepCosts, Settings.HeightmapResolution, Heights, Connectivity);
        AStar search = new AStar(walkable.IsWalkable, neighbours, paths.StepCostsRoad, MapTools.OctileDistance, 2f);
        paths.SetSearch(search);
        search.PrepareSearch(Settings.HeightmapResolution * Settings.HeightmapResolution);
        paths.CreateNetwork(TerrainEdges);
        search.CleanUp();
        ChunkTerrainData.SetHeights(0, 0, paths.Heights);

        ChunkTerrainData.RefreshPrototypes();
        ChunkTerrainData = TerrainLabeler.MapTerrain(Moiisture, Heights, ChunkTerrainData, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, gridCoords * Settings.HeightmapResolution);
        vGen.PaintGras(ChunkSeed, Heights, Settings.Trees, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, ChunkTerrainData);
    }

    public void Flush()
    {
        if (DEBUG_ON)
        {
            FloatImageExporter HimgExp = new FloatImageExporter(0f, 1f);
            IntImageExporter CimgExp = new IntImageExporter(-1, Connectivity.NumLabels - 1);
            HimgExp.Export(string.Format("HeightmapAt{0}-{1}", GridCoords.x, GridCoords.y), Heights);
            CimgExp.Export(string.Format("ConnectivityMapAt{0}-{1}", GridCoords.x, GridCoords.y), Connectivity.Labels);
        }
        if (UnityTerrain != null)
        {
            GameObject.Destroy(UnityTerrain.gameObject);
        }
        UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
        Terrain terrain =  UnityTerrain.GetComponent<Terrain>();
        terrain.materialType = Terrain.MaterialType.Custom;
        terrain.materialTemplate = Settings.TerrainMaterial;
        UnityTerrain.SetActive(true);
        UnityTerrain.transform.Translate(new Vector3(GridCoords.x, 0, GridCoords.y) * Settings.Size);
    }
}