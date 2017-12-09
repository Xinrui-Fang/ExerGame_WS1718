using UnityEngine;
using System.Collections.Generic;
using UtilsInterface;
using PathInterfaces;

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
        if (Connectivity.Labels[start.x, start.y] != Connectivity.Labels[end.x, end.y])
        {
            Debug.Log(string.Format("{0} ({2}) and {1} ({3}) are not connected!", start, end, Connectivity.Labels[start.x, start.y], Connectivity.Labels[end.x, end.y]));
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
        TerrainSmoother.Apply(linePoint.x, linePoint.y);
        RoadFlatten.Apply(linePoint.x, linePoint.y);
    }

    public void AddToStreetMap(List<Vector2Int> path, int width=1)
    {
        foreach (Vector2Int point in path)
        {
            StreetMap[point.x, point.y] = true;
            RoadSmooth(point);
        }
    }
}
