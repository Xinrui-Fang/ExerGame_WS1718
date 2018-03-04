using Assets.Utils;
using Assets.World;
using Assets.World.Heightmap;
using Assets.World.Jumps;
using Assets.World.Paths;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TerrainChunk
{
	public Vector2Int GridCoords;

	public Vector3 TerrainPos;
	public Vector2 TerrainOffset;

	public int ChunkSeed;

	public int FlushedJumps;

	public float[,] Heights, Moisture;
	public int[,] ClusterMap;
	public Vector3[,] Normals;
	public int ClusterCount;
	public Terrain ChunkTerrain;

	private long WorldSeed;
	public TerrainChunkEdge[] TerrainEdges;
	public GameSettings Settings;
	public QuadTree<ObjectData> Objects;
	private TerrainData ChunkTerrainData;

	public float[,,] SplatmapData;

	private VegetationGenerator vGen;
	private PathFinder paths;
	public PathTools.ConnectivityLabel Connectivity;
	public GameObject UnityTerrain;

	public List<TreeInstance> Trees { get; private set; }
	public List<int[,]> DetailMapList { get; internal set; }
	internal List<JumpData> JumpList { get; private set; }
	public bool HasJumps { get; private set; }
	public bool isFinished = false;
	public int GrassProgress = 0;
	public GrasField grasField;

	public TerrainChunk N, E, S, W;
	public override int GetHashCode()
	{
		unchecked
		{
			int hash = GridCoords.x;
			hash = GridCoords.y + hash * 881;
			return hash * 2719 + (int)WorldSeed;
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

	public Vector2Int ToLocalCoordinate(float x, float z)
	{
		int ix = Mathf.RoundToInt((x / (float)Settings.Size - GridCoords.x) * (float)Settings.HeightmapResolution - .5f);
		int iy = Mathf.RoundToInt((z / (float)Settings.Size - GridCoords.y) * (float)Settings.HeightmapResolution - .5f);
		return new Vector2Int(ix, iy);
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
		TerrainPos = new Vector3(GridCoords.x, 0, GridCoords.y) * (float)Settings.Size;
		TerrainOffset = new Vector3(GridCoords.x, GridCoords.y) * (float)Settings.Size;
		ChunkSeed = GetHashCode();

		Objects = new QuadTree<ObjectData>(GetBoundary());
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

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to prepare arrays and edges at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Reset();
		stopWatch.Start();

		Settings.GetHeightMapGenerator(GridCoords * Settings.HeightmapResolution).ManipulateHeight(ref Heights, Settings.HeightmapResolution, Settings.Size);

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create Heightmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

		stopWatch.Reset();
		stopWatch.Start();

		Settings.Moisture.GetHeightSource(GridCoords * Settings.HeightmapResolution, WorldSeed, 99).ManipulateHeight(ref Moisture, Settings.HeightmapResolution, Settings.Size);

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create  Moisture at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

		stopWatch.Reset();
		stopWatch.Start();

		NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / (float)Settings.HeightmapResolution);

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create Normals at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

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

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to prepare pathfinding at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
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
			Assets.Utils.Debug.Log(string.Format("Run Kmeans with {0} clusters for Label of size {1}", clusters, Connectivity.LabelSizes[i]));
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
			for (int x = 0; x < Heights.GetLength(1); x++)
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

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create ConnectivityMap K-Means and ClusterMap at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

		stopWatch.Reset();
		stopWatch.Start();
		paths = new PathFinder(AStarStepCost.StepCosts, Settings.HeightmapResolution, Heights, Connectivity);
		AStar search = new AStar(walkable.IsWalkable, neighbours, paths.StepCostsRoad, MapTools.OctileDistance, 2f);
		paths.SetSearch(search);
		search.PrepareSearch(Settings.HeightmapResolution * Settings.HeightmapResolution);
		paths.CreateNetwork(this, TerrainEdges);
		search.CleanUp();

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create route network at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Reset();
		stopWatch.Start();

		Trees = vGen.PaintGras(this, ChunkSeed, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, Settings.MaxTreeCount, Normals);
		NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / Settings.HeightmapResolution);
		TerrainLabeler.MapTerrain(this, Moisture, Heights, Normals, SplatmapData, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, gridCoords * Settings.HeightmapResolution);

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create Vegetation and Splatmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Stop();

		JumpList = JumpPointFinder.FindJumps(ref paths.paths, 1, Settings.MinJumpDist, Settings.MaxJumpDist, this);
		paths.FinalizePaths(this);

		grasField = new GrasField(this, this.GetHashCode());
	}

	public void CheckNeighbors()
	{
		UnityTerrain.GetComponent<Terrain>().SetNeighbors(
			W != null ? W.UnityTerrain.GetComponent<Terrain>() : null,
			N != null ? N.UnityTerrain.GetComponent<Terrain>() : null,
			E != null ? E.UnityTerrain.GetComponent<Terrain>() : null,
			S != null ? S.UnityTerrain.GetComponent<Terrain>() : null
		);
	}

	public void PushTop(TerrainChunk tile)
	{

		tile.S = this;
		var terrain = tile.UnityTerrain.GetComponent<Terrain>();
		var myterrain = UnityTerrain.GetComponent<Terrain>();
		terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(0, Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1, 1));
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

				Assets.Utils.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
				if (tile.paths.Hub.Contains(alien))
				{
					Assets.Utils.Debug.Log("Found both points.");
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

	public void PushRight(TerrainChunk tile)
	{

		tile.W = this;
		tile.CheckNeighbors();
		var terrain = tile.UnityTerrain.GetComponent<Terrain>();
		var myterrain = UnityTerrain.GetComponent<Terrain>();
		terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, 0, 1, Settings.HeightmapResolution - 1));
		terrain.Flush();

		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[1].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint + (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint + 3 * (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);

				Assets.Utils.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
				if (tile.paths.Hub.Contains(alien))
				{
					Assets.Utils.Debug.Log("Found both points.");
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

		tile.N = this;
		tile.CheckNeighbors();
		var terrain = tile.UnityTerrain.GetComponent<Terrain>();
		var myterrain = UnityTerrain.GetComponent<Terrain>();
		myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(0, Settings.HeightmapResolution - 1, Settings.HeightmapResolution - 1, 1));


		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[0].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint, Settings.HeightmapResolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint + 2 * (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);

				Assets.Utils.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
				if (tile.paths.Hub.Contains(alien))
				{
					Assets.Utils.Debug.Log("Found both points.");
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

		tile.E = this;
		tile.CheckNeighbors();
		var terrain = tile.UnityTerrain.GetComponent<Terrain>();
		var myterrain = UnityTerrain.GetComponent<Terrain>();
		myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(Settings.HeightmapResolution - 1, 0, 1, Settings.HeightmapResolution - 1));

		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[3].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint + 3 * (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint + (Settings.HeightmapResolution - 1), Settings.HeightmapResolution - 1);
				Assets.Utils.Debug.Log(string.Format("Point on this side: {0}; Point on other side: {1}", familiar, alien));
				if (tile.paths.Hub.Contains(alien))
				{
					Assets.Utils.Debug.Log("Found both points.");
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

	[Conditional("DEBUG")]
	public void ExportDebugImages()
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

		Assets.Utils.Debug.Log(string.Format("Took {0} ms to export debug Images at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Stop();
		foreach (var path in paths.paths)
		{
			path.DrawDebugLine(paths.paths.Count);
		}
	}

	private void DestroyJumps() {
		if (!HasJumps) return;
		for (int i = 0; i < JumpList.Count; i++) {
			if ( JumpList[i].Ramp != null) {
				GameObject.Destroy(JumpList[i].Ramp.gameObject);
			}
		}
		HasJumps = false;
	}

	public void Flush(SurfaceManager SM)
	{
		ExportDebugImages();
		if (UnityTerrain != null)
		{
			GameObject.Destroy(UnityTerrain.gameObject);
		}

		ChunkTerrainData = new TerrainData
		{
			heightmapResolution = Settings.HeightmapResolution,
			size = new Vector3(Settings.Size, Settings.Depth, Settings.Size),
			splatPrototypes = GameSettings.SpatProtoTypes,
			alphamapResolution = Settings.HeightmapResolution,
			detailPrototypes = GameSettings.DetailPrototypes,
			treePrototypes = GameSettings.TreeProtoTypes,
			treeInstances = Trees.ToArray(),
			thickness = 10f
		};

		ChunkTerrainData.SetDetailResolution(Settings.DetailResolution, Settings.DetailResolutionPerPatch);
		ChunkTerrainData.RefreshPrototypes();

		ChunkTerrainData.SetHeights(0, 0, Heights);
		ChunkTerrainData.SetAlphamaps(0, 0, SplatmapData);

		Trees.Clear();

		UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
		UnityTerrain.layer = 8;
		UnityTerrain.name = string.Format("TerrainChunk at {0}", GridCoords);

		GameObject SurfaceManagerObject = GameObject.Find("Surface Manager");
		if (SurfaceManagerObject != null)
			UnityTerrain.transform.SetParent(SurfaceManagerObject.transform);


		Terrain terrain = UnityTerrain.GetComponent<Terrain>();
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

		UnityTerrain.transform.position = TerrainPos;
		isFinished = true;
		Synchronize(SM);

		UnityTerrain.SetActive(true);

		FlushedJumps = 0;
		// Cleanup
		Heights = new float[0, 0];
		//Moisture = new float[0, 0];
		Normals = new Vector3[0, 0];
		SplatmapData = new float[0, 0, 0];

		grasField.Flush();
	}
	
	public void DestroyTerrain()
	{
		GameObject.Destroy(UnityTerrain);
	}

	public PathFinder GetPathFinder()
	{
		return paths;
	}
}
