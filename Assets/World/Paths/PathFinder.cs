using UnityEngine;
using System.Collections.Generic;
using PathInterfaces;
using Assets.World.Paths;
using Assets.Utils;
using Assets.World;

public class PathFinder
{
	Vector2Int UpperLimits, LowerLimits;

	public Vector2Int SearchPerimeterUpper { get; private set; }
	public Vector2Int SearchPermieterLower { get; private set; }

	public VertexHub Hub;

	public int[,] StreetMap;
	public float[,] Heights;
	public int Resolution;
	private int RoadSize, RoadFlatArea, RoadSmoothArea;
	private PathTools.ConnectivityLabel Connectivity;
	private IPathSearch SearchAlgo;
	private DGetStepCost StepCosts;
	private MapTools.Flatten RoadFlatten;
	private MapTools.KernelAppliance TerrainSmoother;
	public List<NavigationPath> paths;
	public WayVertex StartingPoint;

	public PathFinder(DGetStepCost stepCosts, int Resolution, float[,] heights, PathTools.ConnectivityLabel connectivity)
	{
		paths = new List<NavigationPath>();
		Hub = new VertexHub();
		StreetMap = new int[Resolution, Resolution];
		Heights = heights;
		RoadFlatArea = 4;
		RoadSmoothArea = 6;
		LowerLimits = new Vector2Int(0, 0);
		UpperLimits = new Vector2Int(Resolution - 1, Resolution - 1);
		this.Resolution = Resolution;
		SearchPerimeterUpper = UpperLimits;
		SearchPermieterLower = LowerLimits;
		StepCosts = stepCosts;
		Connectivity = connectivity;
		RoadFlatten = new MapTools.Flatten(new MapTools.VariableDistCircle(LowerLimits, UpperLimits, 1, RoadFlatArea), Heights);
		TerrainSmoother = new MapTools.KernelAppliance(
			new MapTools.VariableDistCircle(LowerLimits, UpperLimits, 1, RoadSmoothArea),
			new MapTools.VariableDistCircle(LowerLimits, UpperLimits, 1, 2),
			new OctileDistKernel(),
			Heights
		);
	}

	public void SetSearch(IPathSearch search)
	{
		SearchAlgo = search;
	}

	public void MakePath(Vector2Int start, Vector2Int end)
	{
		// Check that start and end are on the same label
		if (Connectivity.Labels[start.y, start.x] != Connectivity.Labels[end.y, end.x])
		{
			Assets.Utils.Debug.Log(string.Format("{0} ({2}) and {1} ({3}) are not connected!", start, end, Connectivity.Labels[start.y, start.x], Connectivity.Labels[end.y, end.x]));
			return;
		}
		// Find the path using the search algorithm
		List<Vector2Int> path = SearchAlgo.Search(ref start, ref end);

		if (path.Count == 0)
		{
			return;
		}
		// Remember Path
		AddToStreetMap(path);
	}

	/* For use with Pathfinding on heightmap.
     * Doubles the cost of traveling outside of streets. Used to enforce reusage of streets.
     */
	public float StepCostsRoad(int ax, int ay, int bx, int by)
	{
		float boundaryFactor = 1f;
		if (ax == 0 || ay == 0 || ax == Resolution - 1 || ay == Resolution - 1) boundaryFactor *= 4f;
		if (bx == 0 || by == 0 || bx == Resolution - 1 || by == Resolution - 1) boundaryFactor *= 4f;

		if (StreetMap[ax, ay] > 0 && StreetMap[bx, by] > 0) return boundaryFactor * StepCosts(ax, ay, bx, by);
		if (StreetMap[ax, ay] > 0 || StreetMap[bx, by] > 0) return boundaryFactor * 2f * StepCosts(ax, ay, bx, by);
		return 4f * boundaryFactor * StepCosts(ax, ay, bx, by);
	}

	/* Smooth terrain around road with the defined Kernels
     * Flatten road itself with another kernel.
     */
	public void RoadSmooth(Vector2Int linePoint)
	{
		TerrainSmoother.Apply(linePoint.y, linePoint.x);
		RoadFlatten.Apply(linePoint.y, linePoint.x);
	}


	/* Add Path to StreetMap. 
     * If path xy intersetcs existing path za at point i split the paths to xi, iy, zi, ia.
     */
	public void AddToStreetMap(List<Vector2Int> path, int width = 1)
	{
		// don't bother with empty path
		if (path.Count == 0) return;
		PathBranchAutomaton.BranchPaths(path, StreetMap, ref Hub, paths);
	}

	public void CreateNetwork(TerrainChunk terrain, TerrainChunkEdge[] terrainEdges)
	{
		List<Vector2Int> DiscardedPoints = new List<Vector2Int>();
		List<List<Vector2Int>> Points = new List<List<Vector2Int>>();
		for (int i = 0; i < Connectivity.NumLabels; i++)
		{
			Points.Add(new List<Vector2Int>());
		}

		int Offset = 0;
		foreach (TerrainChunkEdge edge in terrainEdges)
		{
			//Assets.Utils.Debug.Log(string.Format("Inspecting edge to {0} from {1}", edge.ChunkPos2, edge.ChunkPos1));
			edge.GenerateRoadPoints();
			foreach (int roadPoint in edge.RoadPoints)
			{
				Vector2Int p = MapTools.UnfoldToPerimeter(roadPoint + Offset, Resolution - 1);
				//Assets.Utils.Debug.Log(string.Format("Got point {0} for {1} and Offset {2}", p, roadPoint, Offset));
				int label = Connectivity.Labels[p.y, p.x];
				if (label >= 0)
				{
					Points[label].Add(p);
				}
				else
				{
					DiscardedPoints.Add(p);
				}
			}
			Offset += Resolution - 1;
		}

		for (int i = 0; i < Connectivity.NumLabels; i++)
		{
			//Assets.Utils.Debug.Log(string.Format("Looking at Connectivity group {0}", i));
			if (Points[i].Count == 1)
			{
				DiscardedPoints.Add(Points[i][0]);
			}
			else if (Points[i].Count > 1)
			{
				ConnectPoints(Points[i]);
			}
		}
		int maxLength = 0; // length of the entry path.
		foreach (NavigationPath path in paths)
		{
			path.Finalize(Hub);
			path.Mount();
			if (maxLength < path.Waypoints.Count)
			{
				StartingPoint = path.Start;
			}
			Assets.Utils.LinkedListNode<Vector2Int> node = path.Waypoints.First;
			int i = 0;
			while (node != null)
			{
				bool success = terrain.Objects.Put(
					new QuadTreeData<ObjectData>(terrain.ToWorldCoordinate(node.Value.x, node.Value.y), QuadDataType.street, new ObjectData { collection = path.Label - 1, label = i })
				);
				if (!success)
				{
					Assets.Utils.Debug.Log(string.Format("Could not add Street at {0} to QuadTree with {1}.", node.Value, terrain.Objects.Boundary));
				}
				node = node.Next;
				i++;
			}
			PathStraighter.Straighten(ref path.Waypoints);
			path.TranslateToWorldSpace(terrain);
		}
	}

	public void FinalizePaths(TerrainChunk terrain)
	{
		foreach (NavigationPath path in paths)
		{
			path.Finalize(Hub);
			path.TranslateToWorldSpace(terrain);
		}
		//paths.Clear();
	}

	private void ConnectPoints(List<Vector2Int> Points)
	{
		if (Points.Count == 2)
		{
			//Assets.Utils.Debug.Log("Making paths");
			MakePath(Points[0], Points[1]);
		}
		else
		{
			//Assets.Utils.Debug.Log("Sorting points . . .");
			var comparer = new PointDistanceComparer(Points[0]);
			Points.Sort((a, b) => -1 * comparer.Compare(a, b));
			for (int i = 1; i < Points.Count; i++)
			{
				MakePath(Points[0], Points[i]);
			}
		}
	}

	private class PointDistanceComparer : IComparer<Vector2Int>
	{
		private Vector2Int P;
		public PointDistanceComparer(Vector2Int p)
		{
			P = p;
		}
		public int Compare(Vector2Int a, Vector2Int b)
		{
			//return MapTools.OctileDistance(P.x, P.y, a.x, a.y).CompareTo(MapTools.OctileDistance(P.x, P.y, b.x, b.y));
			return (Mathf.Sqrt((P.x * a.x) * (P.x - a.x) + (P.y - a.y) * (P.y - a.y)).CompareTo(Mathf.Sqrt((P.x - b.x) * (P.x - b.x) + (P.y - b.y) * (P.y - b.y))));
		}
	}
}
