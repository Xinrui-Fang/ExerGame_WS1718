using System;
using System.Collections.Generic;
using Assets.Utils;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.World.Paths
{
    static class JumpPointFinder
    {
        public static void FindJumps(ref List<NavigationPath> paths, ref QuadTree<int> objects, int stepSize, float minDist, float maxDist, float [,] Heights, TerrainChunk chunk)
        {
            int pathCountBefore = paths.Count;
            for (int i = 0; i < pathCountBefore; i++)
            {
                if (paths[i].Waypoints.Count < stepSize * 2 + 2) continue;
                for (int j = stepSize + 1; j < paths[i].Waypoints.Count -2; j += stepSize)
                {
                    // TODO: Raycast on the quadtree if point is a turning point on the path.
                    Vector3 node = paths[i].WorldWaypoints[j];
                    Vector3 NextNode = paths[i].WorldWaypoints[i + 1];
                    Vector2 dest = new Vector2(NextNode.x, NextNode.z);
                    Vector2 origin = new Vector2(node.x, node.z);
                    Vector2 dir = dest - origin;
                    dir.Normalize();

                    origin = origin + minDist * dir;

                    QuadTreeData<int> collision = objects.Raycast(origin, dir, maxDist);
                    if (collision != null)
                    {
                        Vector2Int localCoords = chunk.ToLocalCoordinate(collision.location.x, collision.location.y);
                        Vector3 worldColPos = new Vector3(collision.location.x, Heights[localCoords.y, localCoords.x] * chunk.Settings.Depth, collision.location.y);
                        UnityEngine.Debug.DrawLine(node, worldColPos, Color.black, 1000f);
                    }
                    // Test wheter jummp to tangential direction would be possible.
                }
                // TODO: for all found jump points on this path split the path at that point and add a new jump path to the created wayvertex.
            }
        }
    }
}
