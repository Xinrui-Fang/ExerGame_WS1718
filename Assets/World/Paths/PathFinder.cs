using UnityEngine;
using System.Collections.Generic;
using UtilsInterface;
using PathInterfaces;
using System;

public class PathFinder
{
    Vector2Int UpperLimits, LowerLimits;

    public Vector2Int SearchPerimeterUpper { get; private set; }
    public Vector2Int SearchPermieterLower { get; private set; }

    public bool[,] StreetMap;
    public float[,] Heights;
    public int Resolution;
    private int RoadSize, RoadFlatArea, RoadSmoothArea;
    private PathTools.ConnectivityLabel Connectivity;
    private IPathSearch SearchAlgo;
    private DGetStepCost StepCosts;
    private MapTools.Flatten RoadFlatten;
    private MapTools.KernelAppliance TerrainSmoother;

    public PathFinder(DGetStepCost stepCosts, int Resolution, float[,] heights, PathTools.ConnectivityLabel connectivity)
    {
        StreetMap = new bool[Resolution, Resolution];
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
        Debug.Log(string.Format("## Building Path from {0} to {1}. ##", start, end));
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
        if (StreetMap[ax, ay] && StreetMap[bx, by]) return StepCosts(ax, ay, bx, by);
        if (StreetMap[ax, ay] || StreetMap[bx, by]) return 2f * StepCosts(ax, ay, bx, by);
        return 4f * StepCosts(ax, ay, bx, by);
    }

    public void RoadSmooth(Vector2Int linePoint)
    {
        TerrainSmoother.Apply(linePoint.y, linePoint.x);
        RoadFlatten.Apply(linePoint.y, linePoint.x);
    }

    public void AddToStreetMap(List<Vector2Int> path, int width=1)
    {
        foreach (Vector2Int point in path)
        {
            StreetMap[point.x, point.y] = true;
            RoadSmooth(point);
        }
    }

    internal void CreateNetwork(TerrainChunkEdge[] terrainEdges)
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
            Debug.Log(string.Format("Inspecting edge to {0} from {1}", edge.ChunkPos2, edge.ChunkPos1));
            edge.GenerateRoadPoints();
            Debug.Log(edge.RoadPoints.Count);
            foreach (int roadPoint in edge.RoadPoints)
            {
                Vector2Int p = MapTools.UnfoldToPerimeter(roadPoint + Offset, Resolution - 1);
                Debug.Log(string.Format("Got point {0} for {1} and Offset {2}", p, roadPoint, Offset));
                int label = Connectivity.Labels[p.y, p.x];
                Debug.Log(label);
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
            Debug.Log(string.Format("Looking at Connectivity group {0}", i));
            if (Points[i].Count == 1)
            {
                DiscardedPoints.Add(Points[i][0]);
            }
            else if (Points[i].Count > 1)
            {
                ConnectPoints(Points[i]);
            }
        }
    }

    private void ConnectPoints(List<Vector2Int> Points)
    {
        if(Points.Count == 2)
        {
            Debug.Log("Making paths");
            MakePath(Points[0], Points[1]);
        }
        else
        {
            Debug.Log("Sorting points . . .");
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
