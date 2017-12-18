using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Assets.World.Paths
{
    public struct NavigationPath
    {
        public Vector2Int Start, End;
        public LinkedList<Vector2Int> Waypoints;

        public NavigationPath(List<Vector2Int> points)
        {
            Start = points[0];
            End = points[points.Count - 1];
            Waypoints = new LinkedList<Vector2Int>(points);
        }

        public bool Split(Vector2Int At, ref NavigationPath newPath)
        {
            LinkedListNode<Vector2Int> p = Waypoints.Find(At);
            if (p == null) return false;
            newPath.Start = Start;
            newPath.End = At;
            //Debug.Log(string.Format("Splitting Path of lenght {0} at {1}.", Waypoints.Count, p.Value));
            newPath.Waypoints = new LinkedList<Vector2Int>(Waypoints.TakeWhile((x) => x != At));
            newPath.Waypoints.AddLast(At);
            //Debug.Log(newPath.Waypoints.Count);
            while (Waypoints.First != p)
            {
                Waypoints.Remove(Waypoints.First);
            }
            //Debug.Log(newPath.Waypoints.Count);
            Start = At;
            return true;
        }
    }
}
