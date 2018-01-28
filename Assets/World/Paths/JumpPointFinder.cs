using System;
using System.Collections.Generic;
using Assets.Utils;
using System.Linq;
using System.Text;

namespace Assets.World.Paths
{
    static class JumpPointFinder
    {
        static void FindJumps(ref List<NavigationPath> paths, ref QuadTree<int> objects, int stepSize)
        {
            int pathCountBefore = paths.Count;
            for (int i = 0; i < pathCountBefore; i++)
            {
                if (paths[i].Waypoints.Count < stepSize * 2 + 2) continue;
                for (int j = stepSize + 1; j < paths[i].Waypoints.Count -2; j += stepSize)
                {
                    // TODO: Raycast on the quadtree if point is a turning point on the path.
                    // Test wheter jummp to tangential direction would be possible.
                }
                // TODO: for all found jump points on this path split the path at that point and add a new jump path to the created wayvertex.
            }
        }
    }
}
