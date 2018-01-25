using Assets.Utils;
using Assets.World.Heightmap;
using Assets.World.Paths;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TerrainChunk
{
    public Vector2Int GridCoords;
    public int ChunkSeed;
    public float[,] Heights, Moisture;
    public int[,] ClusterMap;
    public Vector3[,] Normals;
    public int ClusterCount;
    public Terrain ChunkTerrain;

    private long WorldSeed;
    public TerrainChunkEdge[] TerrainEdges;
    public GameSettings Settings;
    public QuadTree<int> Objects;
    private TerrainData ChunkTerrainData;

    public float[,,] SplatmapData;

    private VegetationGenerator vGen;
    private PathFinder paths;
    public PathTools.ConnectivityLabel Connectivity;
    public GameObject UnityTerrain;

    public bool DEBUG_ON = true;

    public List<TreeInstance> Trees { get; private set; }
    public List<int[,]> DetailMapList { get; internal set; }

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

    public RectangleBound GetBoundary()
    {
        float halfSize = (float)Settings.Size / 2f;
        Vector2 center = new Vector2(GridCoords.x + .5f, GridCoords.y + .5f) * (float)Settings.Size;
        return new RectangleBound(center, halfSize);
    }

    public Vector2 ToWorldCoordinate(int x, int y)
    {
        return new Vector2(
            GridCoords.x + ((float)x + .5f) / (float)Settings.HeightmapResolution, 
            GridCoords.y + ((float)y + .5f) / (float)Settings.HeightmapResolution
        ) * (float)Settings.Size;
    }

    public Vector2 ToWorldCoordinate(float x, float y)
    {
        return new Vector2(GridCoords.x + x, GridCoords.y + y) * (float)Settings.Size;
    }

    public void ToWorldCoordinate(int x, int y, ref Vector2 Out)
    {
        Out.x = GridCoords.x + ((float)x + .5f) / (float)Settings.HeightmapResolution;
        Out.y = GridCoords.y + ((float)y + .5f) / (float)Settings.HeightmapResolution;
        Out *= (float)Settings.Size;
    }

    public void ToWorldCoordinate(float x, float y, ref Vector2 Out)
    {
        Out.x = GridCoords.x + x;
        Out.y = GridCoords.y + y;
        Out *= (float)Settings.Size;
    }


    public void Build(Vector2Int gridCoords)
    {
        GridCoords = gridCoords;
        ChunkSeed = GetHashCode();

        Objects = new QuadTree<int>(GetBoundary());
        isFinished = false;
        Stopwatch stopWatch = new Stopwatch();
        stopWatch.Start();
        // Resetting all the Arrays
        Heights = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
        Normals = new Vector3[Settings.HeightmapResolution, Settings.HeightmapResolution];
        Moisture = new float[Settings.HeightmapResolution, Settings.HeightmapResolution];
        SplatmapData = new float[Settings.HeightmapResolution, Settings.HeightmapResolution, GameSettings.SpatProtoTypes.Length];
;
        TerrainEdges = new TerrainChunkEdge[4]
        {
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,-1), WorldSeed, Settings.HeightmapResolution), // S
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(1,0), WorldSeed, Settings.HeightmapResolution), // E
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,1), WorldSeed, Settings.HeightmapResolution), // N
            new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(-1,0), WorldSeed, Settings.HeightmapResolution) // W
        };

        UnityEngine.Debug.Log(string.Format("Took {0} ms to prepare arrays and edges at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Reset();
        stopWatch.Start();

        Settings.GetHeightMapGenerator(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Heights, Settings.HeightmapResolution, Settings.Size);
        Settings.Moisture.GetHeightSource(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Moisture, Settings.HeightmapResolution, Settings.Size);
        NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / (float)Settings.HeightmapResolution);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create Heightmap, Normals and Moisture at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        
        stopWatch.Reset();
        stopWatch.Start();

        Vector2Int lowerBound = new Vector2Int(0, 0);
        Vector2Int upperBound = new Vector2Int(Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1);
        PathTools.Bounded8Neighbours neighbours = new PathTools.Bounded8Neighbours(ref lowerBound, ref upperBound);
        PathTools.NormalYThresholdWalkable walkable_src = new PathTools.NormalYThresholdWalkable(
            Mathf.Cos(Mathf.Deg2Rad * 35),
            Normals, 
            Settings.HeightmapResolution, ref lowerBound, ref upperBound);
        PathTools.CachedWalkable walkable = new PathTools.CachedWalkable(walkable_src.IsWalkable, lowerBound, upperBound, Settings.HeightmapResolution);
        PathTools.Octile8GridSlopeStepCost AStarStepCost = new PathTools.Octile8GridSlopeStepCost(5000, 10, Heights);

        UnityEngine.Debug.Log(string.Format("Took {0} ms to prepare pathfinding at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Reset();
        stopWatch.Start();
        Connectivity = new PathTools.ConnectivityLabel(Settings.HeightmapResolution, neighbours, walkable.IsWalkable);

        
        ClusterMap = new int[Settings.HeightmapResolution, Settings.HeightmapResolution];
        for (int i = 0; i < ClusterMap.GetLength(0); i++)
        {
            for (int j = 0; j < ClusterMap.GetLength(1); j++)
            {
                ClusterMap[i, j] = -1;
            }
        }
        float ClusterSize = ((float)(Settings.HeightmapResolution * Settings.HeightmapResolution)) * .25f;
        int offset = 0;
        int clusters = 0;
        for (int i = 0; i < Connectivity.NumLabels; i++)
        {
            clusters = Mathf.CeilToInt((float)Connectivity.LabelSizes[i] / ClusterSize);
            UnityEngine.Debug.Log(string.Format("Run Kmeans with {0} clusters for Label of size {1}", clusters, Connectivity.LabelSizes[i]));
            KMeansClustering.Cluster(Heights, Connectivity.Labels, ClusterMap, i, ref clusters, offset, Settings.Depth);
            offset += clusters;
        }
        ClusterCount = offset;

        float[] clusterHeight = new float[ClusterCount];
        Vector3[] clusterNormal = new Vector3[ClusterCount];
        float[] clusterMoisture = new float[ClusterCount];
        int[] clusterSize = new int[ClusterCount];

        for (int y = 0; y < Heights.GetLength(0); y++)
        {
            for (int x = 0; x < Heights.GetLength(1); x ++)
            {
                int label = ClusterMap[y, x];
                if (label == -1) continue;
                clusterSize[label]++;
                clusterHeight[label] += Heights[y, x];
                clusterNormal[label] += Normals[y, x];
                clusterMoisture[label] += Moisture[y, x];
            }
        }
        for (int c = 0; c < ClusterCount; c++)
        {
            if (clusterSize[c] <= 1) continue;
            clusterHeight[c] /= clusterSize[c];
            clusterNormal[c] /= clusterSize[c];
            clusterMoisture[c] /= clusterSize[c];
        }

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create ConnectivityMap K-Means and ClusterMap at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        
        stopWatch.Reset();
        stopWatch.Start();
        paths = new PathFinder(AStarStepCost.StepCosts, Settings.HeightmapResolution, Heights, Connectivity);
        AStar search = new AStar(walkable.IsWalkable, neighbours, paths.StepCostsRoad, MapTools.OctileDistance, 2f);
        paths.SetSearch(search);
        search.PrepareSearch(Settings.HeightmapResolution * Settings.HeightmapResolution);
        paths.CreateNetwork(this, TerrainEdges);
        search.CleanUp();

        UnityEngine.Debug.Log(string.Format("Took {0} ms to create route network at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Reset();
        stopWatch.Start();

        
        Trees = vGen.PaintGras(this, ChunkSeed, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, Settings.MaxTreeCount, Normals);
        NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / Settings.HeightmapResolution);
        TerrainLabeler.MapTerrain(this, Moisture, Heights, Normals, SplatmapData, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, gridCoords * Settings.HeightmapResolution);
        
        UnityEngine.Debug.Log(string.Format("Took {0} ms to create Vegetation and Splatmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
        stopWatch.Stop();

        paths.FinalizePaths(this);
    }
    
    public void CheckNeighbors()
    {
        UnityTerrain.GetComponent<Terrain>().SetNeighbors(
            W != null ? W.UnityTerrain.GetComponent<Terrain>(): null,
            N != null ? N.UnityTerrain.GetComponent<Terrain>(): null,
            E != null ? E.UnityTerrain.GetComponent<Terrain>(): null,
            S != null ? S.UnityTerrain.GetComponent<Terrain>(): null
        );
    }
    
    public void PushTop(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.S = this;
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain> ();
        terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(0, Settings.HeightmapResolution -1, Settings.HeightmapResolution -1, 1));
        tile.CheckNeighbors();
        terrain.Flush();
        N = tile;

        // mount paths
        Vector2Int familiar, alien;
        foreach (int roadpoint in TerrainEdges[2].RoadPoints)
        {
            familiar = MapTools.UnfoldToPerimeter(roadpoint + 2 * (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);
            if (paths.Hub.Contains(familiar))
            {
                alien = MapTools.UnfoldToPerimeter(roadpoint, Settings.HeightmapResolution - 1);

                UnityEngine.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
                if (tile.paths.Hub.Contains(alien))
                {
                    UnityEngine.Debug.Log("Found both points.");
                    WayVertex fVertex = paths.Hub.Get(familiar);
                    WayVertex aVertex = tile.paths.Hub.Get(alien);
                    List<PathWithDirection> fpaths = fVertex.GetPaths();
                    List<PathWithDirection> apaths = aVertex.GetPaths();
                    foreach (PathWithDirection p in fpaths)
                    {
                        aVertex.Mount(p.path, p.forward);
                    }

                    foreach (PathWithDirection p in apaths)
                    {
                        fVertex.Mount(p.path, p.forward);
                    }
                }
            }
        }
        //tile.WriteToHeightMap.Release();
    }

    public void PushRight(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.W = this;
        tile.CheckNeighbors();
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, 0, 1, Settings.HeightmapResolution - 1));
        terrain.Flush();
        //tile.WriteToHeightMap.Release();
        // mount paths
        Vector2Int familiar, alien;
        foreach (int roadpoint in TerrainEdges[1].RoadPoints)
        {
            familiar = MapTools.UnfoldToPerimeter(roadpoint + (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);
            if (paths.Hub.Contains(familiar))
            {
                alien = MapTools.UnfoldToPerimeter(roadpoint + 3 * (Settings.HeightmapResolution -1),  Settings.HeightmapResolution - 1);
                
                UnityEngine.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
                if (tile.paths.Hub.Contains(alien))
                {
                    UnityEngine.Debug.Log("Found both points.");
                    WayVertex fVertex = paths.Hub.Get(familiar);
                    WayVertex aVertex = tile.paths.Hub.Get(alien);
                    List<PathWithDirection> fpaths = fVertex.GetPaths();
                    List<PathWithDirection> apaths = aVertex.GetPaths();
                    foreach (PathWithDirection p in fpaths)
                    {
                        aVertex.Mount(p.path, p.forward);
                    }

                    foreach (PathWithDirection p in apaths)
                    {
                        fVertex.Mount(p.path, p.forward);
                    }
                }
            }
        }
    }

    public void PullBotton(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.N = this;
        tile.CheckNeighbors();
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(0, Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1, 1));
        //tile.WriteToHeightMap.Release();

        // mount paths
        Vector2Int familiar, alien;
        foreach (int roadpoint in TerrainEdges[0].RoadPoints)
        {
            familiar = MapTools.UnfoldToPerimeter(roadpoint, Settings.HeightmapResolution - 1);
            if (paths.Hub.Contains(familiar))
            {
                alien = MapTools.UnfoldToPerimeter(roadpoint + 2 * (Settings.HeightmapResolution -1), Settings.HeightmapResolution - 1);

                UnityEngine.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
                if (tile.paths.Hub.Contains(alien))
                {
                    UnityEngine.Debug.Log("Found both points.");
                    WayVertex fVertex = paths.Hub.Get(familiar);
                    WayVertex aVertex = tile.paths.Hub.Get(alien);
                    List<PathWithDirection> fpaths = fVertex.GetPaths();
                    List<PathWithDirection> apaths = aVertex.GetPaths();
                    foreach (PathWithDirection p in fpaths)
                    {
                        aVertex.Mount(p.path, p.forward);
                    }

                    foreach (PathWithDirection p in apaths)
                    {
                        fVertex.Mount(p.path, p.forward);
                    }
                }
            }
        }
    }

    public void PUllLeft(TerrainChunk tile)
    {
        //tile.WriteToHeightMap.WaitOne();
        tile.E = this;
        tile.CheckNeighbors();
        var terrain = tile.UnityTerrain.GetComponent<Terrain>();
        var myterrain = UnityTerrain.GetComponent<Terrain>();
        myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, 0, 1, Settings.HeightmapResolution - 1));
        
        //tile.WriteToHeightMap.Release();

        // mount paths
        Vector2Int familiar, alien;
        foreach (int roadpoint in TerrainEdges[3].RoadPoints)
        {
            familiar = MapTools.UnfoldToPerimeter(roadpoint + 3 * (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);
            if (paths.Hub.Contains(familiar))
            {
                alien = MapTools.UnfoldToPerimeter(roadpoint + (Settings.HeightmapResolution -1), Settings.HeightmapResolution - 1);
                UnityEngine.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
                if (tile.paths.Hub.Contains(alien))
                {
                    UnityEngine.Debug.Log("Found both points.");
                    WayVertex fVertex = paths.Hub.Get(familiar);
                    WayVertex aVertex = tile.paths.Hub.Get(alien);
                    List<PathWithDirection> fpaths = fVertex.GetPaths();
                    List<PathWithDirection> apaths = aVertex.GetPaths();
                    foreach (PathWithDirection p in fpaths)
                    {
                        aVertex.Mount(p.path, p.forward);
                    }

                    foreach (PathWithDirection p in apaths)
                    {
                        fVertex.Mount(p.path, p.forward);
                    }
                }
            }
        }
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

        var myterrain = UnityTerrain.GetComponent<Terrain>();
        myterrain.Flush();
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
            IntImageExporter KMimgExp = new IntImageExporter(-1, (ClusterCount - 1));
            IntImageExporter SimgExp = new IntImageExporter(-1, paths.paths.Count + 1);
            HimgExp.Export(string.Format("HeightmapAt{0}-{1}", GridCoords.x, GridCoords.y), Heights);
            HimgExp.Export(string.Format("MoistureAt{0}-{1}", GridCoords.x, GridCoords.y), Moisture);
            CimgExp.Export(string.Format("ConnectivityMapAt{0}-{1}", GridCoords.x, GridCoords.y), Connectivity.Labels);
            KMimgExp.Export(string.Format("ClusterMapAt{0}-{1}", GridCoords.x, GridCoords.y), ClusterMap);
            SimgExp.Export(string.Format("StreetMapAt{0}-{1}", GridCoords.x, GridCoords.y), paths.StreetMap);
           
            UnityEngine.Debug.Log(string.Format("Took {0} ms to export debug Images at {1}", stopWatch.ElapsedMilliseconds, GridCoords));
            stopWatch.Stop();
            foreach (var path in paths.paths)
            {
                path.DrawDebugLine(paths.paths.Count);
            }
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
            thickness = 10f
        };
        ChunkTerrainData.SetDetailResolution(Settings.DetailResolution, Settings.DetailResolutionPerPatch);
        ChunkTerrainData.RefreshPrototypes();

        ChunkTerrainData.SetHeights(0, 0, Heights);
        ChunkTerrainData.SetAlphamaps(0, 0, SplatmapData);
        /*
         for (int i=0; i < DetailMapList.Count; i++)
        {
            ChunkTerrainData.SetDetailLayer(0, 0, i, DetailMapList[i]);
        }
        */
        
        Trees.Clear();
        
        UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
        UnityTerrain.name = string.Format("TerrainChunk at {0}", GridCoords);

        GameObject SurfaceManagerObject = GameObject.Find("Surface Manager");
        if (SurfaceManagerObject != null)
            UnityTerrain.transform.SetParent(SurfaceManagerObject.transform);

        Terrain terrain =  UnityTerrain.GetComponent<Terrain>();
        terrain.castShadows = true;
        terrain.heightmapPixelError = 6;
        terrain.materialType = Terrain.MaterialType.Custom;
        terrain.materialTemplate = Settings.TerrainMaterial;
        terrain.treeBillboardDistance = Settings.TreeBillBoardDistance;
        terrain.treeDistance = Settings.TreeRenderDistance;
        terrain.detailObjectDistance = Settings.DetailRenderDistance;
        terrain.heightmapPixelError = 12;
        
        terrain.castShadows = true;
        terrain.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
        UnityTerrain.SetActive(true);
        UnityTerrain.transform.position = new Vector3(GridCoords.x, 0, GridCoords.y) * (float) Settings.Size;
        isFinished = true;
        Synchronize(SM);
        GrasField MyGras = new GrasField(this, this.GetHashCode());

        // Cleanup
        Heights = new float[0, 0];
        Moisture = new float[0, 0];
        Normals = new Vector3[0, 0];
        SplatmapData = new float[0, 0, 0];
    }
    
    public void DestroyTerrain()
    {
        GameObject.Destroy(UnityTerrain);
    }

    public PathFinder GetPathFinder(){
        return paths;
    }
}
