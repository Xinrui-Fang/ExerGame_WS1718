using UnityEngine;
using Priority_Queue;
using System.Collections.Generic;

public class PathFinder
{
    private TerrainData terrainData;
    private Vector2Int limits;

    public Dictionary<Vector2Int, int> VDepth { get; private set; }

    public bool[,] StreetMap;
    public float[,] Heights;
    private float Depth;
    private float epsilon;
    float[,] gScore;

    public PathFinder(TerrainData terrainData, float Depth, float epsilon=2f)
    {
        this.terrainData = terrainData;
        limits = new Vector2Int(
            terrainData.heightmapWidth,
            terrainData.heightmapWidth
        );
        Heights = terrainData.GetHeights(0, 0, terrainData.heightmapWidth, terrainData.heightmapHeight);
        StreetMap = new bool[limits.x, limits.y];
        this.Depth = Depth;
        this.epsilon = epsilon;
    }

    public void MakePath(Vector2Int start, Vector2Int end, int Refinements = 0, int StepSize = 3)
    {
        if (!MapTools.InGrid(start, new Vector2(0, 0), limits) || !MapTools.InGrid(end, new Vector2(0, 0), limits))
        {
            Debug.Log(string.Format("{0} or {1} not in grid {2}", start, end, limits));
            return;
        }
        Debug.Log(string.Format("Building Path from {0} to {1} with max. stepsize {2} and {3} refinements.", start, end, StepSize, Refinements));
        List<Vector2Int> path = new List<Vector2Int>() {
            start, end
        };

        Vector2Int old_milestone = new Vector2Int();
        List<Vector2Int> next_path = new List<Vector2Int>();
        int old_size = 0;
        for (int r = 0; r <= Refinements; r++)
        {
            int current_StepSize = Mathf.RoundToInt(StepSize / Mathf.Pow(2f, r));
            bool has_old = false;
            foreach (Vector2Int milestone in path)
            {
                if (has_old)
                {   
                    List<Vector2Int> new_vetices = AStar(old_milestone, milestone, current_StepSize);
                    if (new_vetices.Count > 0)
                    {
                        if (next_path.Contains(new_vetices[0]))
                            new_vetices.RemoveAt(0);
                        next_path.AddRange(new_vetices);
                        old_milestone = next_path[next_path.Count - 1];
                    } else
                    {
                        Debug.Log(string.Format("Did not find path from {0} to {1}", old_milestone, milestone));
                        next_path.Add(old_milestone);
                        next_path.Add(milestone);
                        old_milestone = milestone;
                    }
                }
                else
                    old_milestone = milestone;
                has_old = true;
            }
            path = next_path;
            Debug.Log(string.Format("{0}th refinement. Got path of length {1}", r, path.Count));
            if (path.Count > old_size)
            {
                path[0] = start;
                path[path.Count - 1] = end;
                old_size = path.Count;
            } else
            {
                break;
            }
            next_path = new List<Vector2Int>();
        }
        if (path.Count <= 1)
            Debug.Log("No Path found.");
        AddToStreetMap(path);
    }

    public void AddToStreetMap(List<Vector2Int> path, int width=1)
    {
        bool has_old = false;
        Vector2Int old_point = new Vector2Int();
        foreach (Vector2Int point in path)
        {
            if (has_old)
            {
                foreach (Vector2Int line_point in MapTools.BresenhamLine(old_point, point))
                {
                    List<Vector2Int> nodes_in_radius = new List<Vector2Int>(MapTools.GetNeighbours(line_point, limits, 1, 4, false));
                    float Avg = 0; // nodes_in_radius.Count/2f * Heights[line_point.x, line_point.y];
                    StreetMap[line_point.x, line_point.y] = true;

                    foreach (Vector2Int neighbour in nodes_in_radius)
                    {
                        Avg += Heights[neighbour.x, neighbour.y];
                    }
                    Avg /=  nodes_in_radius.Count;
                    foreach (Vector2Int neighbour in nodes_in_radius)
                    {
                        Heights[neighbour.x, neighbour.y] = Avg;
                    }
                    foreach (Vector2Int neighbour in MapTools.GetCircleNodes(line_point, limits, 1, 2, true))
                    {
                        StreetMap[neighbour.x, neighbour.y] = true;
                    }
                }
            }
            old_point = point;
            has_old = true;
        }
    }

    public List<Vector2Int> ReconstructPath(Vector2Int current, Dictionary<Vector2Int, Vector2Int> CameFrom)
    {
        List<Vector2Int> path = new List<Vector2Int>()
        {
            current
        };
        while(CameFrom.ContainsKey(current))
        {
            current = CameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }

    public List<Vector2Int> AStar(Vector2Int start, Vector2Int end, int stepSize)
    {
        int expectedDepth = Mathf.CeilToInt(OctileDistance(start, end) * epsilon);
        gScore = new float[limits[0], limits[1]];
        float[,] fScore = new float[limits[0], limits[1]];
        float TargetDelta = Mathf.Sqrt(2 * Mathf.Pow(stepSize, 2));
        int TTL = 2 * expectedDepth;
        SimplePriorityQueue<Vector2Int, float> Opened = new SimplePriorityQueue<Vector2Int, float>();
        Dictionary<Vector2Int, Vector2Int> CameFrom = new Dictionary<Vector2Int, Vector2Int>();
        for (int y = 0; y < limits[1]; y++)
        {
            for (int x = 0; x < limits[0]; x++)
            {
                gScore[x, y] = float.PositiveInfinity;
                fScore[x, y] = float.PositiveInfinity;
            }
        }
        VDepth = new Dictionary<Vector2Int, int>();
        HashSet<Vector2Int> closed = new HashSet<Vector2Int>();
        fScore[start.x, start.y] = PathCostHeuristic(start, end, stepSize);
        gScore[start.x, start.y] = 0;

        VDepth[start] = 0;
        Opened.Enqueue(start, fScore[start.x, start.y]);

        while (Opened.Count > 0 && TTL > 0)
        {
            TTL--;
            Vector2Int current = Opened.Dequeue();
            if (Vector2Int.Distance(current, end) < TargetDelta)
                return ReconstructPath(current, CameFrom);
            closed.Add(current);

            IEnumerable<Vector2Int> neighbours;
            if (CameFrom.ContainsKey(current))
                neighbours = GetNeighboursOfInterest(current, CameFrom[current], stepSize);
            else
                neighbours = MapTools.GetNeighbours(current, limits, stepSize);
            foreach (Vector2Int neighbour in neighbours)
            {
                if (closed.Contains(neighbour))
                    continue;
                float tentative_gscore = gScore[current.x, current.y] + StepCosts(current, neighbour, stepSize);
                if (tentative_gscore >= gScore[(int)neighbour.x, (int)neighbour.y])
                    continue;
                CameFrom[neighbour] = current;
                VDepth[neighbour] = VDepth[current] + 1;
                gScore[neighbour.x, neighbour.y] = tentative_gscore;
                float hwn = 1f - VDepth[neighbour] / expectedDepth;
                fScore[neighbour.x, neighbour.y] = gScore[neighbour.x, neighbour.y] + (1+hwn)*PathCostHeuristic(neighbour, end, stepSize);
                if (Opened.Contains(neighbour))
                    Opened.UpdatePriority(neighbour, fScore[neighbour.x, neighbour.y]);
                else
                    Opened.Enqueue(neighbour, fScore[neighbour.x, neighbour.y]);
            }
        }
        return new List<Vector2Int>();
    }
    
    private float GetHeightDiff(Vector2Int a, Vector2Int b)
    {
        float hdiff = Mathf.Abs((Heights[a.x, a.y] - Heights[b.x, b.y])) * Depth;
        return hdiff;
    }

    private IEnumerable<Vector2Int> GetNeighboursOfInterest(Vector2Int self, Vector2Int predecessor, int StepSize)
    {
        foreach (Vector2Int neighbour in MapTools.GetNeighbours(self, limits, StepSize))
        {
            if (Mathf.Abs(neighbour.x - predecessor.x) > StepSize
                || Mathf.Abs(neighbour.y - predecessor.y) > StepSize)
                yield return neighbour;
        }
    }
    
    private float StepCosts(Vector2Int a, Vector2Int b, int stepSize)
    {
        if (a == b) return 0f;
        float StreetBuildingCosts = 1f;
        if (StreetMap[b.x, b.y])
            StreetBuildingCosts *= .25f;
        if (StreetMap[a.x, a.y])
            StreetBuildingCosts *= .25f;

        float h = (Heights[a.x, a.y] - Heights[b.x, b.y]) * terrainData.size[1];
        float d = OctileDistance(a, b);
        float slope = Mathf.InverseLerp(0, .5f, h * h );
        float costs = d * StreetBuildingCosts * (1 + slope);
        //if (a.x == a.y)
        //{
        //    Debug.Log(string.Format("c({0}) = {1}, d = {2}, slope = {3}", a, costs, d, slope));
        //}
        return costs;
    }

    private float PathCostHeuristic(Vector2Int a, Vector2Int b, int stepSize)
    {
        if (a == b) return 0f;
        // Vector3 normal = terrainData.GetInterpolatedNormal(a.x / terrainData.heightmapWidth, a.y / terrainData.heightmapWidth);
        return OctileDistance(a, b);
    }

    private float OctileDistance(Vector2Int a, Vector2Int b)
    {
        float dx = Mathf.Abs(a.x - b.x);
        float dy = Mathf.Abs(a.y - b.y);
        return (dx + dy) + (Mathf.Sqrt(2) - 2f) * Mathf.Min(dx, dy);
    }
}
