using Assets.Utils;
using Assets.World.Heightmap;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TerrainChunk
{
    public Vector2Int GridCoords;
    public int ChunkSeed;
    public float[,] Heights, Moisture;
    public Vector3[,] Normals;
    public Terrain ChunkTerrain;

    private long WorldSeed;
    public TerrainChunkEdge[] TerrainEdges;
    public GameSettings Settings;
    private TerrainData ChunkTerrainData;

    public float[,,] SplatmapData;

    private VegetationGenerator vGen;
    private PathFinder paths;
    public PathTools.ConnectivityLabel Connectivity;
    GameObject UnityTerrain;

    public bool DEBUG_ON = false;

    public List<TreeInstance> Trees { get; private set; }

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
            alphamapResolution = Settings.HeightmapResolution,

            detailPrototypes = Settings.GetDetail(),
            treePrototypes = settings.GetTreePrototypes()
        };
        ChunkTerrainData.SetDetailResolution(Settings.DetailResolution, Settings.DetailResolutionPerPatch);
        ChunkTerrainData.RefreshPrototypes();

        SplatmapData = new float[ChunkTerrainData.alphamapWidth, ChunkTerrainData.alphamapHeight, ChunkTerrainData.alphamapLayers];
        vGen = new VegetationGenerator();
        Trees = new List<TreeInstance>();
    }

    public void Build(Vector2Int gridCoords)
    {

        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();
        // Resetting all the Arrays
        Heights = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
        Normals = new Vector3[Settings.HeightmapResolution, Settings.HeightmapResolution];
        Moisture = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];

        GridCoords = gridCoords;
        ChunkSeed = GetHashCode();
        TerrainEdges = new TerrainChunkEdge[4]
        {
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,-1), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(1,0), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,1), WorldSeed, Settings.HeightmapResolution),
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(-1,0), WorldSeed, Settings.HeightmapResolution)
        };

        UnityEngine.Debug.Log(string.Format("Took {0} ms to prepare arrays and edges at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Reset();
        stopWatch.Start();

        Settings.GetHeightMapGenerator(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Heights, Settings.HeightmapResolution, Settings.Size);
        Settings.Moisture.GetHeightSource(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Moisture, Settings.HeightmapResolution, Settings.Size);
        NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / Settings.HeightmapResolution);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create Heightmap and Moisture at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        
        stopWatch.Reset();
        stopWatch.Start();

        Vector2Int lowerBound = new Vector2Int(0, 0);
        Vector2Int upperBound = new Vector2Int(Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1);
        PathTools.Bounded8Neighbours neighbours = new PathTools.Bounded8Neighbours(ref lowerBound, ref upperBound);
        PathTools.NormalYThresholdWalkable walkable_src = new PathTools.NormalYThresholdWalkable(
            Mathf.Cos(Mathf.Deg2Rad * 25),
            Normals, 
            Settings.HeightmapResolution, ref lowerBound, ref upperBound);
        PathTools.CachedWalkable walkable = new PathTools.CachedWalkable(walkable_src.IsWalkable, lowerBound, upperBound, Settings.HeightmapResolution);
        PathTools.Octile8GridSlopeStepCost AStarStepCost = new PathTools.Octile8GridSlopeStepCost(5000, 10, Heights);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to prepare pathfinding at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Reset();
        stopWatch.Start();
        Connectivity = new PathTools.ConnectivityLabel(Settings.HeightmapResolution, neighbours, walkable.IsWalkable);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create ConnectivityMap at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        
        stopWatch.Reset();
        stopWatch.Start();
        paths = new PathFinder(AStarStepCost.StepCosts, Settings.HeightmapResolution, Heights, Connectivity);
        AStar search = new AStar(walkable.IsWalkable, neighbours, paths.StepCostsRoad, MapTools.OctileDistance, 2f);
        paths.SetSearch(search);
        search.PrepareSearch(Settings.HeightmapResolution * Settings.HeightmapResolution);
        paths.CreateNetwork(TerrainEdges);
        search.CleanUp();

        NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / Settings.HeightmapResolution);
        UnityEngine.Debug.Log(string.Format("Took {0} ms to create route network at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Reset();
        stopWatch.Start();
        
        TerrainLabeler.MapTerrain(Moisture, Heights, Normals, SplatmapData, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, gridCoords * Settings.HeightmapResolution);
        Trees = vGen.PaintGras(ChunkSeed, Heights, Settings.Trees.Length, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, Normals);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create Vegetation and Splatmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Stop();
    }

    public void Flush(SurfaceManager SM)
    {
        if (DEBUG_ON)
        {

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            FloatImageExporter HimgExp = new FloatImageExporter(0f, 1f);
            IntImageExporter CimgExp = new IntImageExporter(-1, Connectivity.NumLabels - 1);
            IntImageExporter SimgExp = new IntImageExporter(0, paths.paths.Count);
            HimgExp.Export(string.Format("HeightmapAt{0}-{1}", GridCoords.x, GridCoords.y), Heights);
            HimgExp.Export(string.Format("MoistureAt{0}-{1}", GridCoords.x, GridCoords.y), Moisture);
            CimgExp.Export(string.Format("ConnectivityMapAt{0}-{1}", GridCoords.x, GridCoords.y), Connectivity.Labels);
            SimgExp.Export(string.Format("StreetMapAt{0}-{1}", GridCoords.x, GridCoords.y), paths.StreetMap);
           
            UnityEngine.Debug.Log(string.Format("Took {0} ms to export debug Images at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
            stopWatch.Stop();
        }
        if (UnityTerrain != null)
        {
            GameObject.Destroy(UnityTerrain.gameObject);
        }
        
        ChunkTerrainData.SetHeights(0, 0, Heights);
        ChunkTerrainData.SetAlphamaps(0, 0, SplatmapData);
        ChunkTerrainData.treeInstances = Trees.ToArray();
        UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
        Terrain terrain =  UnityTerrain.GetComponent<Terrain>();
        terrain.materialType = Terrain.MaterialType.Custom;
        terrain.materialTemplate = Settings.TerrainMaterial;
        UnityTerrain.SetActive(true);
        UnityTerrain.transform.position = new Vector3(GridCoords.x, 0, GridCoords.y) * (float) Settings.Size;       
    }
    
    public void DestroyTerrain()
    {
        GameObject.Destroy(UnityTerrain);
    }
}
