using UnityEngine;
using System.Collections.Generic;
using PathInterfaces;
using Assets.World.Paths;
using Assets.Utils;

public class PathFinder
{
    Vector2Int UpperLimits, LowerLimits;

    public Vector2Int SearchPerimeterUpper { get; private set; }
    public Vector2Int SearchPermieterLower { get; private set; }

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

    public PathFinder(DGetStepCost stepCosts, int Resolution, float[,] heights, PathTools.ConnectivityLabel connectivity)
    {
        paths = new List<NavigationPath>();
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
        //Debug.Log(string.Format("## Building Path from {0} to {1}. ##", start, end));
        if (Connectivity.Labels[start.y, start.x] != Connectivity.Labels[end.y, end.x])
        {
            Debug.Log(string.Format("{0} ({2}) and {1} ({3}) are not connected!", start, end, Connectivity.Labels[start.y, start.x], Connectivity.Labels[end.y, end.x]));
            return;
        }
        List<Vector2Int> path = SearchAlgo.Search(ref start, ref end);
        if (path.Count == 0)
        {
            return;
            //Debug.Log("!FALLBACK Use line for path.");
            //path = new List<Vector2Int>(MapTools.BresenhamOrthogonalLine(start, end));
        }
        AddToStreetMap(path);
    }

    public float StepCostsRoad(int ax, int ay, int bx, int by)
    {
        if (StreetMap[ax, ay] > 0  && StreetMap[bx, by] > 0) return StepCosts(ax, ay, bx, by);
        if (StreetMap[ax, ay] > 0 || StreetMap[bx, by] > 0) return 2f * StepCosts(ax, ay, bx, by);
        return 4f * StepCosts(ax, ay, bx, by);
    }

    public void RoadSmooth(Vector2Int linePoint)
    {
        TerrainSmoother.Apply(linePoint.y, linePoint.x);
        RoadFlatten.Apply(linePoint.y, linePoint.x);
    }

    public void AddToStreetMap(List<Vector2Int> path, int width = 1)
    {
        // don't bother with empty path
        if (path.Count == 0) return;

        //Debug.Log("Adding Path to Streetmap.");

        // load label from start point
        int LastLabel = StreetMap[path[0].x, path[0].y];
        int MyLabel = paths.Count + 1;

        // remember previous point for branching
        Vector2Int oldPoint = path[0];

        NavigationPath MyPath = new NavigationPath()
        {
            Start = path[0],
            Waypoints = new LinkedList<Vector2Int>()
        };
        bool PathOverride = LastLabel == 0;
        foreach (Vector2Int point in path)
        {
            int nextLabel = StreetMap[point.x, point.y];
            //Debug.Log(string.Format("working on point {0} : {1} - {2}, NL {3}, LL {4}", i++, point, paths.Count, nextLabel, LastLabel));
            if (nextLabel == LastLabel)
            {
                if (PathOverride)
                {
                    //Debug.Log("overide");
                    StreetMap[point.x, point.y] = MyLabel;
                    MyPath.Waypoints.AddLast(point);
                }
            }
            else if (nextLabel != 0)
            {
                MyPath.End = point;
                if (MyPath.Start != MyPath.End)
                {
                    paths.Add(MyPath);
                    NavigationPath branch = new NavigationPath();
                    if (paths[StreetMap[point.x, point.y] - 1].Split(point, ref branch))
                    {
                        foreach (Vector2Int waypoint in branch.Waypoints)
                        {
                            StreetMap[waypoint.x, waypoint.y] = paths.Count + 1;
                        }
                        paths.Add(branch);
                    }
                    LastLabel = StreetMap[point.x, point.y];
                    PathOverride = false;
                }
            }
            else
            {
                NavigationPath branch = new NavigationPath();
                //Debug.Log(string.Format("point {0}, paths {1}, Streetmap {2}, LastLabel {3}, nextLabel {4}", oldPoint, paths.Count, StreetMap[oldPoint.x, oldPoint.y], LastLabel, nextLabel));
                if (paths[StreetMap[oldPoint.x, oldPoint.y] - 1].Split(oldPoint, ref branch))
                {
                    foreach (Vector2Int waypoint in branch.Waypoints)
                    {
                        StreetMap[waypoint.x, waypoint.y] = paths.Count + 1;
                    }
                    paths.Add(branch);
                }
                LastLabel = StreetMap[point.x, point.y];
                MyLabel = paths.Count + 1;
                MyPath = new NavigationPath()
                {
                    Start = oldPoint,
                    Waypoints = new LinkedList<Vector2Int>()
                };
                MyPath.Waypoints.AddLast(oldPoint);
                MyPath.Waypoints.AddLast(point);
                StreetMap[point.x, point.y] = MyLabel;
                PathOverride = true;
                LastLabel = 0;
            }
            RoadSmooth(point);
            oldPoint = point;
        }

        MyPath.End = MyPath.Waypoints.Last.Value;
        paths.Add(MyPath);
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
            //Debug.Log(string.Format("Inspecting edge to {0} from {1}", edge.ChunkPos2, edge.ChunkPos1));
            edge.GenerateRoadPoints();
            foreach (int roadPoint in edge.RoadPoints)
            {
                Vector2Int p = MapTools.UnfoldToPerimeter(roadPoint + Offset, Resolution - 1);
                //Debug.Log(string.Format("Got point {0} for {1} and Offset {2}", p, roadPoint, Offset));
                int label = Connectivity.Labels[p.y, p.x];
                if (label >= 0)
                {
                    Points[label].Add(p);                   
                } else
                {
                    DiscardedPoints.Add(p);
                }
            }
            Offset += Resolution - 1;
        }

        for (int i=0; i < Connectivity.NumLabels; i++)
        {
            //Debug.Log(string.Format("Looking at Connectivity group {0}", i));
            if (Points[i].Count == 1)
            {
                DiscardedPoints.Add(Points[i][0]);
            }
            else if (Points[i].Count > 1)
            {
                ConnectPoints(Points[i]);
            }
        }
        foreach (NavigationPath path in paths)
        {

            LinkedListNode<Vector2Int> node = path.Waypoints.First;
            while (node != null)
            {
                bool success = terrain.Objects.Put(
                    new QuadTreeData(terrain.ToWorldCoordinate(node.Value.x, node.Value.y), QuadDataType.street, StreetMap[node.Value.x, node.Value.y])
                );
                if (!success)
                {
                    Debug.Log(string.Format("Could not add Street at {0} to QuadTree with {1}.", node.Value, terrain.Objects.Boundary));
                }
                node = node.Next;
            }
        }
    }

    private void ConnectPoints(List<Vector2Int> Points)
    {
        if(Points.Count == 2)
        {
            //Debug.Log("Making paths");
            MakePath(Points[0], Points[1]);
        }
        else
        {
            //Debug.Log("Sorting points . . .");
            var comparer = new PointDistanceComparer(Points[0]);
            Points.Sort((a,b) => -1* comparer.Compare(a,b));
            for (int  i = 1; i < Points.Count; i++)
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
