using System;
using System.Collections.Generic;
using Assets.Utils;
using System.Linq;
using System.Text;
using UnityEngine;
using Assets.World.Jumps;

namespace Assets.World.Paths
{
	/// <summary>
	/// class to find points suitable for jumping.
	/// </summary>
	static class JumpPointFinder
	{
		private static float jump_y(float vy, float t, float gravity = 9.81f)
		{
			return -.5f * gravity * t * t + vy * t;
		}

		public static List<Vector3> JumpPoints(Vector3 start, Vector3 dir, float vy, float vx, int steps, float t_max, float gravity = 9.81f)
		{
			List<Vector3> points = new List<Vector3>(steps);
			for (float t = 0f; t <= t_max; t += (t_max / (float)steps))
			{
				points.Add(start + dir * t + new Vector3(0, jump_y(vy, t, gravity), 0));
			}
			return points;
		}

		public static bool getPerfectSpeed(Vector3 start, Vector3 end, float gravity, ref float v, ref float t)
		{
			Vector3 dir = new Vector3(end.x - start.x, 0, end.z - start.z);
			float tar_y = end.y - start.y;
			float tar_x = dir.magnitude;

			float a = -gravity * tar_x * tar_x / (2f * (tar_y - tar_x));
			if (a > 0)
			{
				v = (float)Math.Sqrt(a);
				t = tar_x / v;
				return true;
			}
			return false;

		}

		public static int CheckPhysics(Vector3 start, Vector3 end, float speed, ref Vector3 rayTarget, ref Vector3 landingPoint, float gravity = 9.81f)
		{
			// direction of the jump (without y)
			Vector3 dir = new Vector3(end.x - start.x, 0, end.z - start.z);
			float dy = end.y - start.y;
			float x = dir.magnitude;
			dir.Normalize();
			float vx = (float)Math.Cos(Math.PI * 0.25d) * speed;
			float vy = (float)Math.Sin(Math.PI * 0.25d) * speed;
			UnityEngine.Debug.LogFormat("{0}, {1}", vx, vy);
			if (vx == 0) return -1;

			float t = x / vx;
			float y = jump_y(vy, t, gravity);

			float peak_x = vy * vx / (gravity);
			float peak_t = peak_x / vx;
			float peak_y = jump_y(vy, peak_t, gravity);

			UnityEngine.Debug.LogFormat("peakx {0}, dir {1}, peak_y {2}", peak_x, dir, peak_y);
			rayTarget = start + peak_x * dir;
			rayTarget.y += peak_y;

			float intersection_x = 2 * vx * vx / gravity * (vy / vx - dy / x);
			float intersection_t = intersection_x / vx;
			float intersection_y = jump_y(vy, intersection_t, gravity);
			landingPoint = start + intersection_x * dir;
			landingPoint.y += intersection_y;

			UnityEngine.Debug.LogFormat("{0}, {1} {2}", start, rayTarget, end);
			return y > dy ? 1 : (y == dy ? 0 : -1);
		}

		/// <summary>
		/// FindJumps iterates over all pahts of a certain terrainchunk and 
		/// </summary>
		/// <param name="paths"></param>
		/// <param name="objects"></param>
		/// <param name="stepSize"></param>
		/// <param name="minDist"></param>
		/// <param name="maxDist"></param>
		/// <param name="chunk"></param>
		public static List<JumpData> FindJumps(ref List<NavigationPath> paths, ref QuadTree<ObjectData> objects, int stepSize, float minDist, float maxDist, TerrainChunk chunk)
		{
			float gravity = chunk.Settings.Gravity;
			float minSpeed = chunk.Settings.MinJumpSpeed;
			float maxSpeed = chunk.Settings.MaxJumpSpeed;
			int pathCountBefore = paths.Count;

			List<JumpData> JumpList = new List<JumpData>();
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
					if (angle <= 30 || angle >= 150) continue;

					// skip points that have no free space in front of them.
					// TODO: this may be done later after deciding which side is the jump start.
					QuadTreeData<ObjectData> immidiatecollision = objects.Raycast(origin, dir, 3f, .25f);
					if (immidiatecollision != null) continue;
					 

					// start a raycast with minDist distance.
					origin = origin + minDist * .5f * dir;
					Vector3 WorldDir = (NextNode - node).normalized;
					Vector3 WorldOrigin = node + minDist * .5f * WorldDir;

					if (objects.Collides(new CircleBound(origin, 2f))) continue;

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

						if (colPos.y > node.y)
						{
							dir = -dir;
							QuadTreeData<ObjectData> immidiatecollision2 = objects.Raycast(colPos, dir, 2f, .1f);
							if (immidiatecollision2 != null) continue;
							Vector3 Copy = colPos;
							colPos = WorldOrigin;
							WorldOrigin = Copy;
						}

						Vector3 rayMinTarget = new Vector3();
						Vector3 rayMaxTarget = new Vector3();
						Vector3 MinLandingPoint = new Vector3();
						Vector3 MaxLandingPoint = new Vector3();
						int r1 = CheckPhysics(WorldOrigin, colPos, minSpeed, ref rayMinTarget, ref MinLandingPoint, gravity);
						int r2 = CheckPhysics(WorldOrigin, colPos, maxSpeed, ref rayMaxTarget, ref MaxLandingPoint, gravity);
						if (Math.Abs(r1 + r2) <= 1)
						{
							float v = 0;
							float t = 0;
							if (getPerfectSpeed(WorldOrigin, colPos, gravity, ref v, ref t))
							{
								float vx = v;
								v /= (float)Math.Cos(Math.PI * .25f);

								Vector3 rayExactTarget = new Vector3();
								Vector3 ExactLandingPoint = new Vector3();
								int rd = CheckPhysics(WorldOrigin, colPos, v, ref rayExactTarget, ref ExactLandingPoint, gravity);
								JumpList.Add(
									new JumpData()
									{
										PerfectSpeed = v,
										PerfectTime = t,
										LandingPos = ExactLandingPoint,
										RayTarget = rayExactTarget,
										Pos = WorldOrigin,
										Dir = dir,
									}
								);
								chunk.Objects.Put(new QuadTreeData<ObjectData>(
									origin + dir, QuadDataType.jump,
									new ObjectData() { label = JumpList.Count - 1 }
									)
								);
							}
						}

					}
				}
			}
			return JumpList;
		}
	}
}
