using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets.World.Heightmap
{
    static class NormalsFromHeightMap
    {
        public static void GenerateNormals(float[,] heightmap, Vector3[,] NormalMap, float Depth, float StepSize)
        {
            float dx, dy;
            int ysize = heightmap.GetLength(0);
            int xsize = heightmap.GetLength(1);
            for (int y = 0; y < ysize; y++)
            {
                for (int x = 0; x < xsize ; x++)
                {
                    dx = heightmap[y, x < xsize - 1 ? x + 1 : x] - heightmap[y, x > 0 ? x - 1 : x];
                    if (x == 0 || x == xsize - 1)
                    {
                        dx *= 2;
                    }
                    dy = heightmap[y < ysize - 1 ? y + 1: y, x] - heightmap[y > 0 ? y -1 : y, x];
                    if (y == 0 || y == xsize - 1)
                    {
                        dy *= 2;
                    }
                    NormalMap[y, x].x = dx * Depth;
                    NormalMap[y, x].z = dy * StepSize;
                    NormalMap[y, x].y = 2 * Depth;
                    NormalMap[y, x].Normalize();
                }
            }
        }
    }
}
