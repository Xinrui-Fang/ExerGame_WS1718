using UnityEngine;
using System.Collections.Generic;
using MapToolsInterfaces;
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
    private PathSearch SearchAlgo;
    private DGetStepCost StepCosts;

    public PathFinder(DGetStepCost stepCosts, int Resolution, float[,] heights)
    {
        StreetMap = new bool[Resolution, Resolution];
        Heights = heights;
        RoadSize = 0;
        RoadFlatArea = 2;
        RoadSmoothArea = 3;
        LowerLimits = new Vector2Int(0, 0);
        UpperLimits = new Vector2Int(Resolution, Resolution);

        SearchPerimeterUpper = UpperLimits;
        SearchPermieterLower = LowerLimits;
        StepCosts = stepCosts;
    }

    public void SetSearch(PathSearch search)
    {
        SearchAlgo = search;

    }

    public void MakePath(Vector2Int start, Vector2Int end)
    {
        Debug.Log(string.Format("## Building Path from {0} to {1}. ##", start, end));
        List<Vector2Int> path = SearchAlgo.Search(start, end);
        if (path.Count == 0)
        {
            Debug.Log("!FALLBACK Use line for path.");
            path = new List<Vector2Int>(MapTools.BresenhamOrthogonalLine(start, end));
        }
        AddToStreetMap(path);
    }

    public float StepCostsRoad(int ax, int ay, int bx, int by)
    {
        if (StreetMap[ax, ay] && StreetMap[bx, by]) return StepCosts(ax, ay, bx, by);
        if (StreetMap[ax, ay] || StreetMap[bx, by]) return 2f * StepCosts(ax, ay, bx, by);
        return 4f * StepCosts(ax, ay, bx, by);
    }

    public void RoadSmooth(Vector2Int linePoint, IKernel outerKernel, Vector2Int upperLimits, Vector2Int lowerLimits)
    {
        MapTools.SmoothCircular(linePoint, upperLimits, lowerLimits, RoadSmoothArea, Heights, outerKernel);
        MapTools.FlattenCircular(linePoint, upperLimits, lowerLimits, RoadFlatArea, Heights);
        //MapTools.SmoothCircular(linePoint, upperLimits, lowerLimits, RoadSmoothArea, Heights, outerKernel);
    }

    public void AddToStreetMap(List<Vector2Int> path, int width=1)
    {
        IKernel octDistKernel = new OctileDistKernel();
        foreach (Vector2Int point in path)
        {
            StreetMap[point.x, point.y] = true;
            RoadSmooth(point, octDistKernel, UpperLimits, LowerLimits);
            foreach (Vector2Int neighbour in MapTools.GetCircleNodes(point, UpperLimits, LowerLimits, 1, RoadSize, true))
            {
                StreetMap[neighbour.x, neighbour.y] = true;
            }
        }
    }

    public Vector2Int ToLocalCoords(Vector2Int GlobalCoords, int StepSize)
    {
        return new Vector2Int(Mathf.FloorToInt(GlobalCoords.x / (float)StepSize) * StepSize, Mathf.FloorToInt(GlobalCoords.y / (float)StepSize) * StepSize);
    }
}
