using UnityEngine;
using PathInterfaces;
using Priority_Queue;
using System.Collections.Generic;
using System;

public struct PathNode : IEquatable<PathNode>
{
    public int x, y;  
    public int CameFrom;
    public float GScore;
    public bool Closed;
    public bool Walkable;

    public override int GetHashCode()
    {
        unchecked // overflow is not a problem.
        {
            int hash = 977; // prime
            hash *= 881 * x; // 881 is prime
            hash *= 881 * y;
            return hash;
        }
    }

    public bool Equals(PathNode other)
    {
        return this == other;
    }

    public override bool Equals(object obj)
    {
        return obj is PathNode && this == (PathNode)obj;
    }

    public static bool operator ==(PathNode x, PathNode y)
    {
        return x.x == y.x && x.y == y.y;
    }

    public static bool operator !=(PathNode x, PathNode y)
    {
        return !(x == y);
    }
}

public class AStar: IPathSearch
{
    public uint Steps;
    private readonly DIsWalkable Walkable;
    private readonly IGetNeighbors NeighborSource;
    private readonly DGetStepCost RealCosts;
    private readonly DGetStepCost Heuristic;
    private readonly float Epsilon;
    private PathNode[] Nodes;
    private uint nextNodeIDx;
    private bool prepared = false;

    public AStar(DIsWalkable walkable, IGetNeighbors neighbors, DGetStepCost realCosts, DGetStepCost heuristic, float epsilon=0f)
    {
        Walkable = walkable;
        NeighborSource = neighbors;
        RealCosts = realCosts;
        Heuristic = heuristic;
        Epsilon = 1f + epsilon;
    }

    public void PrepareSearch(int ExpectedNodesCount)
    {

        this.Nodes = new PathNode[ExpectedNodesCount];
        prepared = true;
    }

    private bool DirTest(uint current, Location2D nextV)
    {
        if (!nextV.valid) return false;
        if (this.Nodes[current].CameFrom == -1) return true;
        PathNode c = Nodes[current];
        float dx = c.x - Nodes[c.CameFrom].x;
        float dy = c.y - Nodes[c.CameFrom].y;
        float dx2 = nextV.x - c.x;
        float dy2 = nextV.y - c.y;
        float a = Vector2.Angle(new Vector2(dx, dy), new Vector2(dx2, dy2));
        if (a > 46)
        {
            return false;
        }
        return nextV.valid;
    }

    public void CleanUp()
    {
        if (prepared)
            Array.Resize<PathNode>(ref this.Nodes, 0);
        prepared = false;
    }

    public List<Vector2Int> ReconstructPath(uint currentIDx)
    {
        List<Vector2Int> path = new List<Vector2Int>();
        
        path.Add(new Vector2Int(Nodes[currentIDx].x, Nodes[currentIDx].y));
        while (Nodes[currentIDx].CameFrom != -1)
        {
            currentIDx = (uint) Nodes[currentIDx].CameFrom;
            path.Add(new Vector2Int(Nodes[currentIDx].x, Nodes[currentIDx].y));
        }
        //Debug.Log(string.Format("## Found path of lenght {0} in {1} steps. ##", path.Count, Steps));
        path.Reverse();
        return path;
    }

    private uint AddNode(int x, int y, float GScore = float.PositiveInfinity, int CameFrom = -1, bool Closed=false)
    {
        //Debug.Log(string.Format("Node {0}/{1}", this.nextNodeIDx, Nodes.Length -1));
        if (nextNodeIDx >= this.Nodes.Length -1)
        {
            Debug.Log("Doubled the AStar Node storage space.");
            Array.Resize<PathNode>(ref this.Nodes, this.Nodes.Length * 2);
        }
        this.Nodes[nextNodeIDx].x = x;
        this.Nodes[nextNodeIDx].y = y;
        this.Nodes[nextNodeIDx].GScore = GScore;
        this.Nodes[nextNodeIDx].CameFrom = -1;
        this.Nodes[nextNodeIDx].Closed = false;
        return this.nextNodeIDx++;
    } 

    public List<Vector2Int> Search(ref Vector2Int start, ref Vector2Int end)
    {
        Debug.Log(string.Format("Searching for path from {0} to {1}", start, end));
        SimplePriorityQueue<uint> Opened = new SimplePriorityQueue<uint>();
        Dictionary<Location2D, uint> NodeCache = new Dictionary<Location2D, uint>();
        Location2D[] NeighBors = NeighborSource.AllocateArray();
        this.nextNodeIDx = 0;

        Location2D startL = Location2D.FromVector2Int(start);
        Location2D endL = Location2D.FromVector2Int(end);
        if (!Walkable(end.x, end.y))
        {
            //Debug.Log("End node is not walkable");
            return new List<Vector2Int>();
        }
        uint current, next, endIdx;
        current = AddNode(start.x, start.y, 0);
        Nodes[current].CameFrom = -1;
        endIdx = AddNode(end.x, end.y);
        Nodes[endIdx].Walkable = Walkable(end.x, end.y);
        NodeCache[startL] = current;
        NodeCache[endL] = endIdx;
        Opened.Enqueue(current, 0f); // FCost does not matter for first node.
        //float dist = MapTools.OctileDistance(start, end);
        Steps = 0;
        while (Opened.Count > 0)
        {
            Steps++;
            current = Opened.Dequeue();
            //Debug.Log(string.Format("Traversing ({0}, {1})", Nodes[current].x, Nodes[current].y));
            if (current == endIdx) 
                return ReconstructPath(current);

            Nodes[current].Closed = true;
            NeighborSource.GetNeighbors(Nodes[current].x, Nodes[current].y, ref NeighBors);
            foreach (Location2D nextV in NeighBors)
            {
                bool followsDir = DirTest(current, nextV);
                if (!(nextV.valid && followsDir)) continue; // skip if e.g. neighbor is out of grid.
                if (NodeCache.ContainsKey(nextV))
                {
                    next = (uint) NodeCache[nextV];
                    //Debug.Log(string.Format("{0} was in cache as {1}.", nextV, next));
                }
                else
                {
                    //Debug.Log(string.Format("{0} is not in cache.", nextV));
                    next = AddNode(nextV.x, nextV.y);
                    Nodes[next].Walkable = Walkable(nextV.x, nextV.y);
                    NodeCache.Add(nextV, next);
                    //Debug.Log(string.Format("Added {0} to cache at {1}.", nextV, next));
                }
                if ((!Nodes[next].Closed) && Nodes[next].Walkable)
                {
                    //Debug.Log(string.Format("{0} Is Walkable and not Closed!", next));
                    float tentative_gscore = Nodes[current].GScore + RealCosts(Nodes[current].x, Nodes[current].y, Nodes[next].x, Nodes[next].y);
                    //Debug.Log(string.Format("GScore({0}, {1}) = {2}, GScore({3}, {4}) = {5}, Tentative GScore({3}, {4}) = {6}", Nodes[current].x, Nodes[current].y, Nodes[current].GScore, Nodes[next].x, Nodes[next].y, Nodes[next].GScore, tentative_gscore));
                    if (tentative_gscore < Nodes[next].GScore)
                    {
                        Nodes[next].CameFrom = (int) current;
                        Nodes[next].GScore = tentative_gscore;
                        float FScore = Nodes[next].GScore + Heuristic(Nodes[next].x, Nodes[next].y, Nodes[endIdx].x, Nodes[endIdx].y) * Epsilon;
                        if (Opened.Contains(next))
                        {
                            Opened.UpdatePriority(next, FScore);
                            //Debug.Log(string.Format("Updated Priority to {1}, #Opened {0} ", Opened.Count, FScore));
                        }
                        else
                        {
                            Opened.Enqueue(next, FScore);
                            //Debug.Log(string.Format("Enqueued Node with prio {1}, #Opened {0} ", Opened.Count, FScore));
                        }
                    }
                }    
            }
        }
        //Debug.Log(string.Format("## Found no path in {0} steps. ##", Steps));
        return new List<Vector2Int>();
    }
    
}