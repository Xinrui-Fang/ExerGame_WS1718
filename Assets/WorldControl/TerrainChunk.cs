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
    public Terrain ChunkTerrain;

    private long WorldSeed;
    public TerrainChunkEdge[] TerrainEdges;
    public GameSettings Settings;
    private TerrainData ChunkTerrainData;

    public float[,,] SplatmapData;

    private VegetationGenerator vGen;
    private PathFinder paths;
    public PathTools.ConnectivityLabel Connectivity;
    public GameObject UnityTerrain;

    public bool DEBUG_ON = false;

    public List<TreeInstance> Trees { get; private set; }
    public bool isFinished = false;

    public TerrainChunk N, E, S, W;
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
        vGen = new VegetationGenerator();
        Heights = new float[0, 0];
        SplatmapData = new float[0, 0, 0];
        Trees = new List<TreeInstance>(0);
    }

    public void Build(Vector2Int gridCoords)
    {
        isFinished = false;
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();
        // Resetting all the Arrays
        Heights = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
        Vector3[,] Normals = new Vector3[Settings.HeightmapResolution, Settings.HeightmapResolution];
        Moisture = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
        SplatmapData = new float[Settings.HeightmapResolution, Settings.HeightmapResolution, Settings.SplatMaps.Length];

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
        Trees = vGen.PaintGras(ChunkSeed, Heights, Settings.Trees.Length, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, Settings.MaxTreeCount, Normals);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create Vegetation and Splatmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Stop();
    }
    
    public void CheckNeighbors()
    {
        if (N != null && S != null && W != null && E != null && isFinished)
        {
            UnityTerrain.GetComponent<Terrain>().SetNeighbors(
                W.UnityTerrain.GetComponent<Terrain>(),
                N.UnityTerrain.GetComponent<Terrain>(),
                E.UnityTerrain.GetComponent<Terrain>(),
                S.UnityTerrain.GetComponent<Terrain>()
            );
        }
    }
    
    public void PushTop(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.S = this;
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain> ();
        terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(0, Settings.HeightmapResolution -1, Settings.HeightmapResolution -1, 1));
        terrain.Flush();
        N = tile;
        tile.CheckNeighbors();
        //tile.WriteToHeightMap.Release();
    }

    public void PushRight(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.W = this;
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, 0, 1, Settings.HeightmapResolution - 1));
        terrain.Flush();
        tile.CheckNeighbors();
        //tile.WriteToHeightMap.Release();
    }

    public void PullBotton(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.N = this;
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(0, Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1, 1));
        //tile.WriteToHeightMap.Release();
    }

    public void PUllLeft(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.E = this;
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, 0, 1, Settings.HeightmapResolution - 1));
        tile.CheckNeighbors();
        //tile.WriteToHeightMap.Release();
    }

    public void PullCorner(TerrainChunk tile, int x, int y)
    {
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        myterrain.terrainData.SetHeights(x, y, terrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1, 1, 1));
    }

    public void PushCroner(TerrainChunk tile, int x, int y)
    {
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        terrain.terrainData.SetHeights(x, y, myterrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1, 1, 1));
    }

    public void Synchronize(SurfaceManager SM)
    {
        // Push Master Values
        var N = SM.GetTile(GridCoords + new Vector2Int(0, 1));
        var E = SM.GetTile(GridCoords + new Vector2Int(1, 0));
        var S = SM.GetTile(GridCoords + new Vector2Int(0, -1));
        var W = SM.GetTile(GridCoords + new Vector2Int(-1, 0));

        if (N != null && N.isFinished)
        {
            PushTop(N);
        }
        if (E != null && E.isFinished)
        {
            PushRight(E);
        }
        if (S != null && S.isFinished)
        {
            PullBotton(S);
        }
        if (W != null && W.isFinished)
        {
            PUllLeft(W);
        }
        CheckNeighbors();
        
        var NE = SM.GetTile(GridCoords + new Vector2Int(1, 1));
        var SW = SM.GetTile(GridCoords + new Vector2Int(-1, -1));

        if (NE != null) PushCroner(NE, 0, 0);
        if (SW != null) PullCorner(SW, 0, 0);
    }

    public void Flush(SurfaceManager SM)
    {
        //
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

        ChunkTerrainData = new TerrainData
        {
            heightmapResolution = Settings.HeightmapResolution,
            size = new Vector3(Settings.Size, Settings.Depth, Settings.Size),
            splatPrototypes = Settings.GetSplat(),
            alphamapResolution = Settings.HeightmapResolution,
            detailPrototypes = Settings.GetDetail(),
            treePrototypes = Settings.GetTreePrototypes(),
            treeInstances = Trees.ToArray(),
        };
        ChunkTerrainData.SetDetailResolution(Settings.DetailResolution, Settings.DetailResolutionPerPatch);
        ChunkTerrainData.RefreshPrototypes();

        ChunkTerrainData.SetHeights(0, 0, Heights);
        ChunkTerrainData.SetAlphamaps(0, 0, SplatmapData);

        Heights = new float[0, 0];
        SplatmapData = new float[0, 0, 0];
        Trees.Clear();
        
        UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
        Terrain terrain =  UnityTerrain.GetComponent<Terrain>();
        terrain.materialType = Terrain.MaterialType.Custom;
        terrain.materialTemplate = Settings.TerrainMaterial;
        terrain.treeBillboardDistance = Settings.TreeBillBoardDistance;
        terrain.treeDistance = Settings.TreeRenderDistance;
        terrain.detailObjectDistance = Settings.DetailRenderDistance;
        UnityTerrain.SetActive(true);
        UnityTerrain.transform.position = new Vector3(GridCoords.x, 0, GridCoords.y) * (float) Settings.Size;
        isFinished = true;
        Synchronize(SM);
    }
    
    public void DestroyTerrain()
    {
        GameObject.Destroy(UnityTerrain);
    }

    public PathFinder GetPathFinder(){
        return paths;
    }
}
