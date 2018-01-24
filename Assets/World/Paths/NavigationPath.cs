using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using Assets.Utils;

namespace Assets.World.Paths
{
    /** A finite Automaton to add a new path to the already exisiting network.
     * 
     **/
    public static class PathBranchAutomaton
    {
        public static void BranchPaths(List<Vector2Int> path, int[,] StreetMap, ref VertexHub Hub, List<NavigationPath> pathStorage)
        {
            // check sanity
            int lastLabel, Status;
            if (path.Count() == 0) return;

            // initialisation
            NavigationPath rawPath = new NavigationPath() { Waypoints = new Utils.LinkedList<Vector2Int>(path) };
            Utils.LinkedListNode<Vector2Int> node = rawPath.Waypoints.First;
            rawPath.Label = pathStorage.Count() + 1;
            lastLabel = StreetMap[node.Value.x, node.Value.y];
            Status = lastLabel == 0 ? 0 : 1;
            if (Status == 0)
            {
                rawPath.Start = Hub.Get(node.Value);
            }

            // Begin traversing automaton
            while (node != null)
            {
                if (Status == -2) break;
                if (Status == -1) // Status -1 is an upredicted outcome
                {
                    Debug.Log(string.Format("Got Status -1 when branching path {0} -> {1}", path[0], path[path.Count - 1]));
                    //recover to this point.
                    //rawPath.Start.Unmount(rawPath);
                    if (rawPath.Start != null && rawPath.End != null) rawPath.Unmount();
                    rawPath.End = Hub.Get(node.Value);
                    rawPath.Mount();
                    pathStorage.Add(rawPath);
                    break;
                }

                int label = StreetMap[node.Value.x, node.Value.y];
                if (Status == 0) // status 1 denotes normal traversal of the path. 
                {
                    if (label == 0) // we can continue with traversing.
                    {
                        Mark(node, rawPath.Label, StreetMap);
                    } else // we have to follow an existing path.
                    {
                        BranchOn(node, label, rawPath, ref Hub, pathStorage, StreetMap, ref lastLabel, ref Status);
                        if (Status >= 0)
                            node = rawPath.Waypoints.First.Next;
                        continue;
                    }
                }
                else if (Status == 1) // we are following an existing path
                {
                    if (label == 0) // we can continue normal traversal
                    {
                        BranchOff(node, rawPath, Hub, pathStorage, StreetMap, ref lastLabel, ref Status);
                        node = node.Next;
                        continue;
                    } else if (label != lastLabel) // we have to switch to following another path.
                    {
                        BranchSwitch(node, rawPath, Hub, pathStorage, StreetMap, label, ref lastLabel, ref Status);
                        node = node.Next;
                        continue;
                    } else // we continue following the path.
                    {
                        // Trim nodes that are on the branch that we are reusing.
                        if (rawPath.Waypoints.First != node)
                            rawPath.Waypoints.Count--;
                            rawPath.Waypoints.First = node;
                            rawPath.Waypoints.First.Previous = null;
                    }
                }
                lastLabel = label;
                node = node.Next;
            }
            // When done add the path to the vertices and pathStorage
            if (rawPath.Waypoints.Last != null)
            {
                rawPath.Start = Hub.Get(rawPath.Waypoints.First.Value);
                rawPath.End = Hub.Get(rawPath.Waypoints.Last.Value);
                rawPath.Mount();
                pathStorage.Add(rawPath);
            }
        }

        /* Marks Streetmap at node coordinates as visited by this path.
         */
        private static void Mark(Utils.LinkedListNode<Vector2Int> node, int label, int[,] StreetMap)
        {
            StreetMap[node.Value.x, node.Value.y] = label;
        }

        private static void SplitAndRemount(int label, Vector2Int Value, List<NavigationPath> pathStorage, VertexHub Hub, int[,] Streetmap, ref NavigationPath CollisionBranch)
        {
            pathStorage[label - 1].Unmount();
            if (pathStorage[label - 1].Split(Value, ref CollisionBranch, ref Hub))
            {
                pathStorage[label - 1].Finalize(Hub);
                // remember split.
                CollisionBranch.Label = pathStorage.Count() + 1;
                Utils.LinkedListNode<Vector2Int> BranchNode = CollisionBranch.Waypoints.First;
                while (BranchNode != null)
                {
                    Mark(BranchNode, CollisionBranch.Label, Streetmap);
                    BranchNode = BranchNode.Next;
                } 
                pathStorage.Add(CollisionBranch);
                CollisionBranch.Finalize(Hub);
            } else
            {
                pathStorage[label - 1].Finalize(Hub);
            }
        }

        /* Called when exiting a path that was previously followed. 
         * The point of exit needs to be turned to a WayVertex e.g the path we were following needs to be split.
         * Prepares rawPath for status 0
         */
        private static void BranchOff(Utils.LinkedListNode<Vector2Int> node, NavigationPath rawPath, VertexHub Hub, List<NavigationPath> pathStorage, int[,] Streetmap, ref int lastLabel, ref int status)
        {
            // TODO: needs to be created a node.previous instead
            if (node.Previous != null)
            {
                // Create WayVertex at node.Previous.Value and split paths.
                NavigationPath CollisionBranch = new NavigationPath();
                SplitAndRemount(lastLabel, node.Previous.Value, pathStorage, Hub, Streetmap, ref CollisionBranch);

                // set remainder of path.
                rawPath.Waypoints.First = node.Previous;
                rawPath.Waypoints.First.Previous = null;
                rawPath.Start = Hub.Get(rawPath.Waypoints.First.Value);
            } else // TODO: investigate is it possible that node.previous is not set?
            {
                rawPath.Waypoints.First = node;
                rawPath.Start = Hub.Get(node.Value);
            }

            rawPath.Label = pathStorage.Count + 1;
            status = 0;
            lastLabel = 0;
        }
        
        /* Called when the new path switches from following one existing path to following another existing path. 
         * Makes sure that the two paths are connected.
         */
        private static void BranchSwitch(Utils.LinkedListNode<Vector2Int> node, NavigationPath rawPath, VertexHub Hub, List<NavigationPath> pathStorage, int[,] Streetmap, int label, ref int lastLabel, ref int status)
        {
            // init
            WayVertex V, W;
            V = W = null;

            
            // Check if one of the points is a WayVertex if so ignore it and continue onwards
            if (Hub.Contains(node.Value))
            {
                V = Hub.Get(node.Value);
                if (V.Connects(label, lastLabel))
                {
                    status = 1;
                    lastLabel = label;
                    return;
                }
            }
            if (node.Previous != null && Hub.Contains(node.Value))
            {
                W = Hub.Get(node.Previous.Value);
                if (W.Connects(label, lastLabel))
                {
                    status = 1;
                    lastLabel = label;
                    return;
                }
            }
            // Obtain missing WayVertices by splitting the two paths.
            if (V == null)
            {
                NavigationPath branch = new NavigationPath();
                SplitAndRemount(label, node.Value, pathStorage, Hub, Streetmap, ref branch);
                V = Hub.Get(node.Value);
            }
            if (W == null)
            {
                NavigationPath branch = new NavigationPath();
                SplitAndRemount(lastLabel, node.Previous.Value, pathStorage, Hub, Streetmap, ref branch);
                W = Hub.Get(node.Value);
            }
            if (V == null || V == null) return;
            // Create missing link path.
            Utils.LinkedList<Vector2Int> pW = new Utils.LinkedList<Vector2Int>();
            pW.AddLast(W.Pos);
            pW.AddLast(V.Pos);
            NavigationPath p = new NavigationPath()
            {
                Start = W,
                End = V,
                Waypoints = pW,
                Label = pathStorage.Count + 1
            };
            pathStorage.Add(p);
            p.Mount();

            // Adjust reminder of path.
            rawPath.Waypoints.First = node;
            node.Previous = null;
            rawPath.Label = pathStorage.Count + 1;
        }

        private static void BranchOn(Utils.LinkedListNode<Vector2Int> node, int label, NavigationPath rawPath, ref VertexHub Hub, List<NavigationPath> pathStorage, int[,] Streetmap, ref int lastLabel, ref int status)
        {
            bool WayVertexExists = Hub.Contains(node.Value);
            // finalize previous path
            NavigationPath NextRawPath = new NavigationPath();
            Vector2Int value = new Vector2Int(node.Value.x, node.Value.y);
            if (rawPath.Start != null && rawPath.End != null) rawPath.Unmount();
            if (rawPath.SplitAt(ref node, ref NextRawPath, ref Hub))
            {
                rawPath.Finalize(Hub);
                pathStorage.Add(rawPath);
                rawPath = NextRawPath;
            } else if (rawPath.Waypoints.Last != null && rawPath.Waypoints.Last.Value == value)
            {
                rawPath.Finalize(Hub);
                pathStorage.Add(rawPath);
                rawPath = new NavigationPath();
                status = -2;
            }

            WayVertex V = Hub.Get(node.Value); // V is Vertex at Node position
            // split colision
            if (!WayVertexExists)
            {
                NavigationPath CollisionBranch = new NavigationPath();
                pathStorage[label - 1].Unmount();
                SplitAndRemount(label, node.Value, pathStorage, Hub, Streetmap, ref CollisionBranch);
            }

            if (rawPath.Waypoints.Count > 1) {
                Vector2Int NextVal = rawPath.Waypoints.First.Next.Value;
                if (Streetmap[NextVal.x, NextVal.y] == 0)
                {
                    status = 0;
                    lastLabel = 0;
                    rawPath.Label = pathStorage.Count() + 1;
                    return;
                }

                foreach (PathWithDirection dp in V.GetPaths())
                {
                    if (dp.path.Waypoints.First.Next.Value == NextVal)
                    {
                        lastLabel = dp.path.Label;
                        status = 1;
                        return;
                    }
                }

            }
            status = -1;
            return;
        }
    }

    /**
     * Wrapper around a Dictionary Vector2Int -> WayVertex.
     * Used to Find points to connect paths to.
     **/
    public class VertexHub
    {
        private Dictionary<Vector2Int, WayVertex> Storage;

        public void Cleanup()
        {
            foreach(WayVertex V in Storage.Values)
            {
                V.Cleanup();
            }
        }

        public VertexHub()
        {
            Storage = new Dictionary<Vector2Int, WayVertex>();
        }

        public WayVertex Get(Vector2Int pos)
        {
            if (!Storage.ContainsKey(pos))
            {
                Storage[pos] = new WayVertex(pos);
            }
            return Storage[pos];
        } 

        public bool Contains (Vector2Int pos)
        {
            return Storage.ContainsKey(pos);
        }
    }
    
    public struct PathWithDirection
    {
        public NavigationPath path;
        public bool forward;

        public PathWithDirection(NavigationPath path, bool forward)
        {
            this.path = path;
            this.forward = forward;
        }

        public override int GetHashCode()
        {
            return path.GetHashCode();
        }
    }

    public class WayVertex: IEquatable<WayVertex>
    {
        public Vector2Int Pos;
        private HashSet<PathWithDirection> Paths;

        public WayVertex(Vector2Int pos)
        {
            Pos = pos;
            Paths = new HashSet<PathWithDirection>();
        }

        public bool Unmount(NavigationPath path)
        {
            PathWithDirection dpath = new PathWithDirection(path, true);
            if (Paths.Contains(dpath))
            {
                Paths.Remove(dpath);
                return true;
            }
            Debug.Log(string.Format("Could not remove path {0} {1} because it is not in WayVertex", path.Start.Pos, path.End.Pos));
            return false;
        }

        public bool Mount(NavigationPath path, bool forward = true)
        {
            PathWithDirection dpath = new PathWithDirection(path, forward);
            if (Paths.Contains(dpath)) return false;
            Paths.Add(dpath);
            return true;
        }

        public bool Connects(int labelA, int labelB)
        {
            bool foundA, foundB;
            foundA = foundB = false;
            foreach (PathWithDirection p in GetPaths())
            {
                if (p.forward)
                {
                    foundA = foundA | p.path.Label == labelA;
                    foundB = foundB | p.path.Label == labelB;
                }
                if (foundA && foundB) return true;
            }
            return false;
        }

        public List<PathWithDirection> GetPaths()
        {
            return Paths.ToList<PathWithDirection>();
        }

        public List<PathWithDirection> GetPaths(PathWithDirection ExcPath)
        {
            List<PathWithDirection> paths = Paths.ToList<PathWithDirection>();

            if (paths.Count > 1)
            {
                paths.Remove(ExcPath);
            }
            return paths;
        }


        public PathWithDirection GetLongest()
        {
            PathWithDirection longest = new PathWithDirection();
            int longestCount = 0;
            Debug.Log(string.Format("Get Longest Path for WayVertex at {0} with {1} paths in total.", Pos, Paths.Count));
            
            foreach (PathWithDirection dpath in Paths.AsEnumerable())
            {
                if (Paths.Count == 1) return dpath;
                if (dpath.path.WorldWaypoints.Length > longestCount)
                {
                    longest = dpath;
                    longestCount = dpath.path.WorldWaypoints.Length;
                }
            }
            return longest;
        }

        public PathWithDirection GetLongest(PathWithDirection ExcPath)
        {
            PathWithDirection longest = new PathWithDirection();
            int longestCount = 0;
            Debug.Log(string.Format("Get Longest Path for WayVertex at {0} with {1} paths in total.", Pos, Paths.Count));
            foreach (PathWithDirection dpath in Paths.AsEnumerable())
            {
                if (Paths.Count == 1) return dpath;
                if (dpath.GetHashCode() == ExcPath.GetHashCode()) continue;
                if (dpath.path.WorldWaypoints.Length > longestCount)
                {
                    longest = dpath;
                    longestCount = dpath.path.WorldWaypoints.Length;
                }
            }
            return longest;
        }

        internal int Count()
        {
            return Paths.Count;
        }

        public bool Equals(WayVertex other)
        {
            return Pos.Equals(other.Pos);
        }

        internal void Cleanup()
        {
            foreach(PathWithDirection p in GetPaths())
            {
                // remove paths that have been added wrongly.
                if (p.path.Waypoints.Count == 0) Unmount(p.path);
                if (p.path.Start != this && p.path.End != this)
                {
                    Unmount(p.path);
                    p.path.Mount();
                }
                if (p.path.Waypoints.First.Value != this.Pos && p.path.Waypoints.Last.Value != this.Pos) {
                    Unmount(p.path);
                    p.path.Mount();
                    }
            }
        }
    }

    public class NavigationPath: IEquatable<NavigationPath>
    {
        public WayVertex Start, End;
        public Utils.LinkedList<Vector2Int> Waypoints;
        public Vector3[] WorldWaypoints; // Waypoints in World Space
        public int Label { get; set; }

        public NavigationPath()
        {
            Start = End = null;
            Waypoints = new Utils.LinkedList<Vector2Int>();
            WorldWaypoints = new Vector3[0];
            Label = -1;
        }

        public NavigationPath(WayVertex Start)
        {
            this.Start = Start;
            End = null;
            Waypoints = new Utils.LinkedList<Vector2Int>();
            WorldWaypoints = new Vector3[0];
            Label = -1;
        }

        public NavigationPath(List<Vector2Int> points)
        {
            Start = new WayVertex(points[0]);
            End = new WayVertex(points[points.Count - 1]);
            Waypoints = new Utils.LinkedList<Vector2Int>(points);
            WorldWaypoints = new Vector3[0];
            Label = -1;
        }

        public bool Mount()
        {
            if (Start == null || End == null)
            {
                Debug.Log(String.Format("Cannot mount path with missing start or end point."));
                return false;
            }
            bool success = true;
            success = success && Start.Mount(this);
            success = success && End.Mount(this, false);
            return success;
        }

        public bool Unmount()
        {
            bool success = true;
            if (Start != null) success = success && Start.Unmount(this);
            if (End != null) success = success && End.Unmount(this);
            return success;
        }

        public void Finalize(VertexHub Hub)
        {
            Waypoints.RevalidateCount();
            if (Waypoints.Count == 0 || Waypoints.First == null || Waypoints.Last == null) return;
            if (Start != null && Start.Pos != Waypoints.First.Value) {
                Start.Unmount(this);
                Start = null;
            }
            if (Start == null)
            {
                Start = Hub.Get(Waypoints.First.Value);
            }
            if (End != null && End.Pos != Waypoints.Last.Value)
            {
                End.Unmount(this);
                End = null;
            }
            if (End == null)
                End = Hub.Get(Waypoints.Last.Value);
            this.Mount();
        }

        public void TranslateToWorldSpace(TerrainChunk chunk)
        {
            Vector2 FlatWorldCoordinate = new Vector2();
            Utils.LinkedListNode<Vector2Int> node = Waypoints.First;
            Waypoints.RevalidateCount();
            WorldWaypoints = new Vector3[Waypoints.Count];
            int i = 0;
            while (node != null)
            {
                chunk.ToWorldCoordinate(
                    (float)node.Value.x / chunk.Settings.HeightmapResolution, 
                    (float)node.Value.y / chunk.Settings.HeightmapResolution,
                    ref FlatWorldCoordinate
                );

                WorldWaypoints[i++].Set(
                    FlatWorldCoordinate.x,
                    chunk.Heights[(int)node.Value.y, (int)node.Value.x] * chunk.Settings.Depth,
                    FlatWorldCoordinate.y
                );
                node = node.Next;
            }
        }

        public override int GetHashCode()
        {
            unchecked // overflow is not a problem.
            {
                return Start.GetHashCode() + End.GetHashCode();
            }
        }

        /** Draws a line representing the world coordinates 
         * Not thread safe Debug method.
         * WARNING: Only run this on the main thread.
         * 
         * parameter: 
         *  pathsTotal (int): Number of paths being drawn in total. Used for distincitve color mapping.
         */
        public void DrawDebugLine(int pathsTotal, float duration = 250f, bool zTest=false, float delta=.1f)
        {
            Color MyColor = Colors.RainbowColor(Label, pathsTotal + 1);
            Vector3 Offset = new Vector3(
                delta * ((float)(Label) / (float)(pathsTotal + 1)), 
                1f, 
                delta * ((float)(Label) / (float)(pathsTotal + 1))
            );
            for (int i=1; i<WorldWaypoints.Length; i++)
            {
                Debug.DrawLine(
                    WorldWaypoints[i - 1] + Offset, 
                    WorldWaypoints[i]     + Offset, 
                    MyColor, 
                    duration,
                    zTest
                );
            }
        }

        public bool Equals(NavigationPath other)
        {
            return (Start == other.Start && End == other.End) || (Start == other.End && End == other.Start);
        }

        public bool SplitAt(ref Assets.Utils.LinkedListNode<Vector2Int> At, ref NavigationPath newPath, ref VertexHub Hub)
        {
            int length = Waypoints.Count;
            if (Waypoints.SplitAt(ref At, ref newPath.Waypoints, true))
            {
                // set path boundaries
                End = Hub.Get(Waypoints.Last.Value);
                Start = Hub.Get(Waypoints.First.Value);
                newPath.Start = Hub.Get(newPath.Waypoints.First.Value);
                newPath.End = Hub.Get(newPath.Waypoints.Last.Value);
                return true;
            }
            else return false;
        }

        public bool Split(Vector2Int Value, ref NavigationPath newPath, ref VertexHub Hub)
        {
            if (Waypoints.SplitAt(Value, ref newPath.Waypoints, true))
            {
                // set path boundaries
                End = Hub.Get(Waypoints.Last.Value);
                Start = Hub.Get(Waypoints.First.Value);
                newPath.Start = Hub.Get(newPath.Waypoints.First.Value);
                newPath.End = Hub.Get(newPath.Waypoints.Last.Value);
                return true;
            }
            else return false;
        }
    }
}
