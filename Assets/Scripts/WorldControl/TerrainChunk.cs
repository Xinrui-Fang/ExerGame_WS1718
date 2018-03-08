using Assets.Utils;
using Assets.World;
using Assets.World.Heightmap;
using Assets.World.Jumps;
using Assets.World.Paths;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TerrainChunk
{
	public Vector2Int GridCoords;

	public Vector3 TerrainPos;
	public Vector2 TerrainOffset;

	public int ChunkSeed;

	public int Resolution { get; private set; }

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
	public int LOD { get; private set; }

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
			GridCoords.x + ((float)x + .5f) / (float)Resolution,
			GridCoords.y + ((float)y + .5f) / (float)Resolution
		) * (float)Settings.Size;
	}

	public Vector2 ToWorldCoordinate(float x, float y)
	{
		return new Vector2(GridCoords.x + x, GridCoords.y + y) * (float)Settings.Size;
	}

	public Vector2Int ToLocalCoordinate(float x, float z)
	{
		int ix = Mathf.RoundToInt((x / (float)Settings.Size - GridCoords.x) * (float)Resolution - .5f);
		int iy = Mathf.RoundToInt((z / (float)Settings.Size - GridCoords.y) * (float)Resolution - .5f);
		return new Vector2Int(ix, iy);
	}

	public void ToWorldCoordinate(int x, int y, ref Vector2 Out)
	{
		Out.x = GridCoords.x + ((float)x + .5f) / (float)Resolution;
		Out.y = GridCoords.y + ((float)y + .5f) / (float)Resolution;
		Out *= (float)Settings.Size;
	}

	public void ToWorldCoordinate(float x, float y, ref Vector2 Out)
	{
		Out.x = GridCoords.x + x;
		Out.y = GridCoords.y + y;
		Out *= (float)Settings.Size;
	}


	public void Build(Vector2Int gridCoords, Vector3 PlayerPos, int? ForceLOD = null)
	{
		if (ForceLOD == null)
		{
			LOD = Settings.RetrieveLOD(gridCoords, PlayerPos);
		}
		else
		{
			if (ForceLOD.Value >= Settings.TerrainLOD.Length)
			{
				LOD = Settings.TerrainLOD.Length - 1;
			}
			else if (ForceLOD.Value < 0)
			{
				LOD = 0;
			}
			else
			{
				LOD = ForceLOD.Value;
			}

		}
		GridCoords = gridCoords;
		TerrainPos = new Vector3(GridCoords.x, 0, GridCoords.y) * (float)Settings.Size;
		TerrainOffset = new Vector3(GridCoords.x, GridCoords.y) * (float)Settings.Size;
		ChunkSeed = GetHashCode();

		Resolution = Settings.TerrainLOD[LOD].HeightmapResolution;
		Objects = new QuadTree<ObjectData>(GetBoundary());
		isFinished = false;
		/**
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		**/
		// Resetting all the Arrays
		Heights = new float[Resolution, Resolution];
		Normals = new Vector3[Resolution, Resolution];
		Moisture = new float[Resolution, Resolution];
		SplatmapData = new float[Resolution, Resolution, GameSettings.SpatProtoTypes.Length];
		;
		TerrainEdges = new TerrainChunkEdge[4]
		{
			new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,-1), WorldSeed, Resolution), // S
			new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(1,0), WorldSeed, Resolution), // E
			new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(0,1), WorldSeed, Resolution), // N
			new TerrainChunkEdge(GridCoords, GridCoords + new Vector2Int(-1,0), WorldSeed, Resolution) // W
		};

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to prepare arrays and edges at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Reset();
		stopWatch.Start();
		**/
		Settings.GetHeightMapGenerator(GridCoords * (Resolution - 1)).ManipulateHeight(ref Heights, Resolution, Settings.Size);

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create Heightmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

		stopWatch.Reset();
		stopWatch.Start();
		**/
		Settings.Moisture.GetHeightSource(GridCoords * (Resolution - 1), WorldSeed, 99).ManipulateHeight(ref Moisture, Resolution, Settings.Size);

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create  Moisture at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

		stopWatch.Reset();
		stopWatch.Start();
		**/
		NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / (float)Resolution);

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create Normals at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);

		stopWatch.Reset();
		stopWatch.Start();
		**/
		Vector2Int lowerBound = new Vector2Int(0, 0);
		Vector2Int upperBound = new Vector2Int(Resolution - 1, Resolution - 1);
		PathTools.Bounded8Neighbours neighbours = new PathTools.Bounded8Neighbours(ref lowerBound, ref upperBound);
		PathTools.NormalYThresholdWalkable walkable_src = new PathTools.NormalYThresholdWalkable(
			Mathf.Cos(Mathf.Deg2Rad * 40),
			Normals,
			Resolution, ref lowerBound, ref upperBound);
		PathTools.CachedWalkable walkable = new PathTools.CachedWalkable(walkable_src.IsWalkable, lowerBound, upperBound, Resolution);
		PathTools.Octile8GridSlopeStepCost AStarStepCost = new PathTools.Octile8GridSlopeStepCost(5000, 10, Heights);

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to prepare pathfinding at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Reset();
		stopWatch.Start();
		**/
		Connectivity = new PathTools.ConnectivityLabel(Resolution, neighbours, walkable.IsWalkable);


		if (Settings.TerrainLOD[LOD].HasStreets)
		{
			/**
			ClusterMap = new int[Resolution, Resolution];
			for (int i = 0; i < ClusterMap.GetLength(0); i++)
			{
				for (int j = 0; j < ClusterMap.GetLength(1); j++)
				{
					ClusterMap[i, j] = -1;
				}
			}
			float ClusterSize = ((float)(Resolution * Resolution)) * .25f;
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
			
			**/
			/**
			Assets.Utils.Debug.Log(string.Format("Took {0} ms to create ConnectivityMap K-Means and ClusterMap at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
			
			stopWatch.Reset();
			stopWatch.Start();
			**/

		}
		paths = new PathFinder(AStarStepCost.StepCosts, Resolution, Heights, Connectivity);

		if (Settings.TerrainLOD[LOD].HasStreets)
		{
			AStar search = new AStar(walkable.IsWalkable, neighbours, paths.StepCostsRoad, MapTools.OctileDistance, 2f);
			paths.SetSearch(search);
			search.PrepareSearch(Resolution * Resolution);
			paths.CreateNetwork(this, TerrainEdges);
			search.CleanUp();

			/**
			Assets.Utils.Debug.Log(string.Format("Took {0} ms to create route network at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
			stopWatch.Reset();
			stopWatch.Start();
			**/
		}
		if (Settings.TerrainLOD[LOD].HasTrees)
		{
			Trees = vGen.GenerateTrees(this, ChunkSeed, paths.StreetMap, Normals);
			NormalsFromHeightMap.GenerateNormals(Heights, Normals, Settings.Depth, (float)Settings.Size / Resolution);
		}
		TerrainLabeler.MapTerrain(this, Moisture, Heights, Normals, SplatmapData, paths.StreetMap, Settings.WaterLevel, Settings.VegetationLevel, gridCoords * Resolution);

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to create Vegetation and Splatmap at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Stop();
		**/

		if (Settings.TerrainLOD[LOD].HasJumps)
			JumpList = JumpPointFinder.FindJumps(ref paths.paths, 1, Settings.MinJumpDist, Settings.MaxJumpDist, this);

		paths.FinalizePaths(this);

		if (Settings.TerrainLOD[LOD].hasGras)
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

	private static int Small2BigCoord(int coord, float TranslationFactor)
	{
		int coordOut = (int)(coord * TranslationFactor) - 1;
		coordOut = coordOut < 0 ? 0 : coordOut;
		return coordOut;
	}

	public static void CopyHeightMapEdge(TerrainChunk from, int fxbase, int fybase, int fwidth, int fheight, TerrainChunk to, int txbase, int tybase)
	{
		var toTerrain = to.UnityTerrain.GetComponent<Terrain>();
		var fromTerrain = from.UnityTerrain.GetComponent<Terrain>();
		if (from.Resolution == to.Resolution)
		{
			toTerrain.terrainData.SetHeights(txbase, tybase, fromTerrain.terrainData.GetHeights(fxbase, fybase, fwidth, fheight));
		}
		return;
		float TranslationFactor = (from.Resolution - 1) / (float)(to.Resolution - 1);
		float[,] fromdata = fromTerrain.terrainData.GetHeights(fxbase, fybase, fwidth, fheight);
		int theight = fheight > 1 ? Mathf.FloorToInt(fheight / TranslationFactor) : 1;
		int twidth = fwidth > 1 ? Mathf.FloorToInt(fwidth / TranslationFactor) : 1;
		float[,] todata = new float[theight, twidth];
		if (fheight == 1)
		{
			if (TranslationFactor > 1f)
			{
				for (int i = 0; i < twidth; i++)
				{
					todata[0, i] = fromdata[0, Small2BigCoord(i, TranslationFactor)];
				}
			}
			else
			{
				for (int i = 0; i < fwidth; i++)
				{
					int prev = i > 0 ? i - 1 : 0;
					int next = i < fwidth - 1 ? i + 1 : fwidth - 1;
					int Bprev = Small2BigCoord(prev, TranslationFactor);
					int Bnext = Small2BigCoord(next, TranslationFactor);
					int n = Bnext - Bprev;
					for (int k = Bprev; k < Bnext; k++)
					{
						todata[0, k] = fromdata[0, i];
					}
				}
			}
		}
		else if (fwidth == 1)
		{
			if (TranslationFactor > 1f)
			{
				for (int i = 0; i < theight; i++)
				{
					todata[i, 0] = fromdata[Small2BigCoord(i, TranslationFactor), 0];
				}
			}
			else
			{
				for (int i = 0; i < fheight; i++)
				{
					int prev = i > 0 ? i - 1 : 0;
					int next = i < fheight - 1 ? i + 1 : fheight - 1;
					int Bprev = Small2BigCoord(prev, TranslationFactor);
					int Bnext = Small2BigCoord(next, TranslationFactor);
					int n = Bnext - Bprev;
					for (int k = Bprev; k < Bnext; k++)
					{
						todata[k, 0] = fromdata[i, 0];
					}
				}
			}

		}
		else
		{
			throw new NotImplementedException();
		}
		toTerrain.terrainData.SetHeights(txbase, tybase, todata);
	}

	public void PushTop(TerrainChunk tile)
	{

		tile.S = this;
		var terrain = tile.UnityTerrain.GetComponent<Terrain>();
		var myterrain = UnityTerrain.GetComponent<Terrain>();
		//terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(0, Resolution - 1, Resolution - 1, 1));
		CopyHeightMapEdge(this, 0, Resolution - 1, Resolution - 1, 1, tile, 0, 0);
		tile.CheckNeighbors();
		terrain.Flush();
		N = tile;

		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[2].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint + 2 * (Resolution - 1), Resolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint, Resolution - 1);

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
		//terrain.terrainData.SetHeights(0, 0, myterrain.terrainData.GetHeights(Resolution - 1, 0, 1, Resolution - 1));
		CopyHeightMapEdge(this, Resolution - 1, 0, 1, Resolution - 1, tile, 0, 0);
		terrain.Flush();

		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[1].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint + (Resolution - 1), Resolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint + 3 * (Resolution - 1), Resolution - 1);

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
		//myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(0, Resolution - 1, Resolution - 1, 1));
		CopyHeightMapEdge(tile, 0, tile.Resolution - 1, tile.Resolution - 1, 1, this, 0, 0);

		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[0].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint, Resolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint + 2 * (Resolution - 1), Resolution - 1);

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
		//myterrain.terrainData.SetHeights(0, 0, terrain.terrainData.GetHeights(Resolution - 1, 0, 1, Resolution - 1));
		CopyHeightMapEdge(tile, tile.Resolution - 1, 0, 1, tile.Resolution - 1, this, 0, 0);
		// mount paths
		Vector2Int familiar, alien;
		foreach (int roadpoint in TerrainEdges[3].RoadPoints)
		{
			familiar = MapTools.UnfoldToPerimeter(roadpoint + 3 * (Resolution - 1), Resolution - 1);
			if (paths.Hub.Contains(familiar))
			{
				alien = MapTools.UnfoldToPerimeter(roadpoint + (Resolution - 1), Resolution - 1);
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
		myterrain.terrainData.SetHeights(x, y, terrain.terrainData.GetHeights(tile.Resolution - 1, tile.Resolution - 1, 1, 1));
	}

	public void PushCroner(TerrainChunk tile, int x, int y)
	{
		var terrain = tile.UnityTerrain.GetComponent<Terrain>();
		var myterrain = UnityTerrain.GetComponent<Terrain>();
		terrain.terrainData.SetHeights(x, y, myterrain.terrainData.GetHeights(Resolution - 1, Resolution - 1, 1, 1));
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
		/**
		Stopwatch stopWatch = new Stopwatch();
		stopWatch.Start();
		**/
		FloatImageExporter HimgExp = new FloatImageExporter(0f, 1f);
		IntImageExporter CimgExp = new IntImageExporter(-1, Connectivity.NumLabels - 1);
		IntImageExporter KMimgExp = new IntImageExporter(-1, (ClusterCount - 1));
		IntImageExporter SimgExp = new IntImageExporter(-1, paths.paths.Count + 1);
		HimgExp.Export(string.Format("HeightmapAt{0}-{1}", GridCoords.x, GridCoords.y), Heights);
		HimgExp.Export(string.Format("MoistureAt{0}-{1}", GridCoords.x, GridCoords.y), Moisture);
		CimgExp.Export(string.Format("ConnectivityMapAt{0}-{1}", GridCoords.x, GridCoords.y), Connectivity.Labels);
		KMimgExp.Export(string.Format("ClusterMapAt{0}-{1}", GridCoords.x, GridCoords.y), ClusterMap);
		SimgExp.Export(string.Format("StreetMapAt{0}-{1}", GridCoords.x, GridCoords.y), paths.StreetMap);

		/**
		Assets.Utils.Debug.Log(string.Format("Took {0} ms to export debug Images at {1}", stopWatch.ElapsedMilliseconds, GridCoords), LOGLEVEL.META);
		stopWatch.Stop();
		**/
		foreach (var path in paths.paths)
		{
			path.DrawDebugLine(paths.paths.Count);
		}
	}

	private void DestroyJumps()
	{
		if (!HasJumps) return;
		for (int i = 0; i < JumpList.Count; i++)
		{
			if (JumpList[i].Ramp != null)
			{
				GameObject.Destroy(JumpList[i].Ramp.gameObject);
			}
		}
		HasJumps = false;
	}

	private void GenerateItems(Terrain terrain)
	{
		System.Random rnd = new System.Random();
		for (int i = 0; i < paths.paths.Count; i++)
		{
			var points = paths.paths[i].Waypoints;
			var iter = points.First;
			while (iter != null)
			{
				if (rnd.Next(0, 100) <= 10)
				{
					var pos2D = ToWorldCoordinate(iter.Value.x, iter.Value.y);
					var position = new Vector3(pos2D.x, 0.0f, pos2D.y);

					position.y = terrain.SampleHeight(position);

					var o = UnityEngine.Object.Instantiate(Settings.ItemTypes[rnd.Next(0, Settings.ItemTypes.Length)].Appearance);
					o.transform.position = position;
				}
				iter = iter.Next;
			}
		}
	}

	public void Flush(SurfaceManager SM)
	{
		//ExportDebugImages();
		if (UnityTerrain != null)
		{
			GameObject.Destroy(UnityTerrain.gameObject);
		}

		ChunkTerrainData = new TerrainData
		{
			heightmapResolution = Resolution,
			size = new Vector3(Settings.Size, Settings.Depth, Settings.Size),
			splatPrototypes = GameSettings.SpatProtoTypes,
			alphamapResolution = Resolution,
			detailPrototypes = GameSettings.DetailPrototypes,
			treePrototypes = GameSettings.TreeProtoTypes,
			treeInstances = Trees.ToArray(),
			thickness = 10f
		};

		ChunkTerrainData.SetDetailResolution(0, 8);
		ChunkTerrainData.RefreshPrototypes();

		ChunkTerrainData.SetHeights(0, 0, Heights);
		ChunkTerrainData.SetAlphamaps(0, 0, SplatmapData);

		Trees.Clear();

		UnityTerrain = Terrain.CreateTerrainGameObject(ChunkTerrainData);
		UnityTerrain.layer = 8;
		UnityTerrain.name = string.Format("TerrainChunk at {0} LOD:{1}", GridCoords, LOD);

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

		// Generate items
		GenerateItems(terrain);

		// Flush Gras
		if (Settings.TerrainLOD[LOD].hasGras)
			grasField.Flush();

		FlushedJumps = 0;
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

	public PathFinder GetPathFinder()
	{
		return paths;
	}
}
