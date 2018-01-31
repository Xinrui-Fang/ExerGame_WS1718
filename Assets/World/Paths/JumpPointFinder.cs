using System;
using System.Collections.Generic;
using Assets.Utils;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.World.Paths
{
	/// <summary>
	/// class to find points suitable for jumping.
	/// </summary>
	static class JumpPointFinder
	{
		/// <summary>
		/// FindJumps iterates over all pahts of a certain terrainchunk and 
		/// </summary>
		/// <param name="paths"></param>
		/// <param name="objects"></param>
		/// <param name="stepSize"></param>
		/// <param name="minDist"></param>
		/// <param name="maxDist"></param>
		/// <param name="chunk"></param>
		public static void FindJumps(ref List<NavigationPath> paths, ref QuadTree<ObjectData> objects, int stepSize, float minDist, float maxDist, TerrainChunk chunk)
		{
			int pathCountBefore = paths.Count;
			for (int i = 0; i < pathCountBefore; i++)
			{
				if (paths[i].Waypoints.Count < stepSize * 2 + 2) continue;
				for (int j = stepSize + 1; j < paths[i].Waypoints.Count - 2; j += stepSize)
				{
					Vector3 PrevNode = paths[i].WorldWaypoints[j - 1];
					Vector3 node = paths[i].WorldWaypoints[j];
					Vector3 NextNode = paths[i].WorldWaypoints[j + 1];

					Vector2 prev = new Vector2(PrevNode.x, PrevNode.z);
					Vector2 dest = new Vector2(NextNode.x, NextNode.z);
					Vector2 origin = new Vector2(node.x, node.z);

					Vector2 dir = origin - prev;
					dir.Normalize();

					// skip points that are no turning points.
					float angle = Vector2.Angle((dest - origin).normalized, dir);
					if (angle <= 20 || angle >= 160) continue;

					// skip points that have no free space in front of them.
					// TODO: this may be done later after deciding which side is the jump start.
					QuadTreeData<ObjectData> immidiatecollision = objects.Raycast(origin, dir, 2f, .1f);
					if (immidiatecollision != null) continue;

					// start a raycast with minDist distance.
					origin = origin + minDist * dir;

					QuadTreeData<ObjectData> collision = objects.Raycast(origin, dir, maxDist - minDist, QuadDataType.street, .5f);
					if (collision != null)
					{
						NavigationPath colpath = paths[collision.contents.collection];
						Vector3 colPos = colpath.WorldWaypoints[collision.contents.label];
						int next_label = collision.contents.label + 1;
						if (collision.contents.label == colpath.WorldWaypoints.Length - 1) { next_label -= 2; }
						Vector2 colnode = new Vector2(colPos.x, colPos.z);
						Vector2 colNext = new Vector2(colpath.WorldWaypoints[next_label].x, colpath.WorldWaypoints[next_label].z);
						Vector3 colDir = (colNext - colnode).normalized;
						if (Vector3.Distance(colPos, node) < minDist) continue;
						float jumpdirAngle = Vector2.Angle(dir, colDir);
						if (jumpdirAngle > 45 && jumpdirAngle < 135) continue;
						UnityEngine.Debug.DrawLine(node, colPos, Color.black, 1000f, false);
						UnityEngine.Debug.DrawLine(node, node + minDist * (colPos - node).normalized, Color.grey, 1000f, false);
						UnityEngine.Debug.DrawLine(node, node + .5f * (colPos - node), Color.red, 1000f, false);
					}
					// Test wheter jummp to tangential direction would be possible.
				}
				// TODO: for all found jump points on this path split the path at that point and add a new jump path to the created wayvertex.
			}
		}
	}
}
