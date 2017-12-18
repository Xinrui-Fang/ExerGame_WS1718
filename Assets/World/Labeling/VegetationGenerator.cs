using UnityEngine;
using System.Collections.Generic;
using Assets.World.Heightmap;

public class VegetationGenerator
{
    public VegetationGenerator(){}

    public List<TreeInstance> PaintGras(long Seed, float[,] Heights, int NumOfTrees, int[,] streetMap, float WaterLevel, float VegetationMaxHeight, Vector3[,] Normals)
    {
        int x_res, y_res;
        x_res = Heights.GetLength(1);
        y_res = Heights.GetLength(0);
        bool[,] TreeMap = new bool[y_res, x_res];
        System.Random prng = new System.Random((int)Seed);
        List<TreeInstance> trees = new List<TreeInstance>(256);
        int n = 0;
        for (int i = 0; i < 128; i++)
        {
            int x, y;

            x = prng.Next(0, x_res);
            y = prng.Next(0, y_res);
            if (streetMap[x, y] > 0 || TreeMap[x,y] || Heights[y,x] <= WaterLevel || Heights[y,x] > VegetationMaxHeight) continue;
            if (Normals[y, x].y < .7f) continue;
            float heightScale = (float)prng.Next(256, 512) / 512.0f;
            trees.Add(new TreeInstance
            {
                prototypeIndex = prng.Next(0, NumOfTrees - 1),
                position = new Vector3((float)x / x_res,
                        Heights[y,x],
                        (float)y / y_res),
                heightScale = heightScale,
                widthScale = heightScale,
                rotation = (float)prng.Next(0, 360) * Mathf.Deg2Rad
            });
            n++;
            TreeMap[x, y] = true;

        }
        /**
        float y_01, x_01;
        int y_hm, x_hm;
        
        float step = 1f / terrainData.detailWidth;
	    for (int l = 0; l < terrainData.detailPrototypes.Length; l++)
        {

            int[,] detailMap = new int[terrainData.detailWidth, terrainData.detailHeight];
            y_01 = 0f;
            for (int y = 0; y < terrainData.detailHeight; y++)
            {
                y_01 += step;
                y_hm = (int)(y_01 * (float)terrainData.heightmapHeight);
                x_01 = 0;
                for (int x = 0; x < terrainData.detailWidth; x++)
                {
                    x_01 += step;
                    x_hm = (int)(x_01 * (float)terrainData.heightmapWidth);
                    if (terrainData.GetInterpolatedNormal(x_01, y_01).y >= .8f && Heights[y_hm, x_hm] > WaterLevel && Heights[y_hm, x_hm] <= VegetationMaxHeight)
                    {
                        int r = prng.Next(-200, 8);
                        if(!TreeMap[x_hm, y_hm] && !streetMap[x_hm, y_hm]) detailMap[y, x] = r > 0 ? r : 0;
                    }
                }
            }
            terrainData.SetDetailLayer(0, 0, l, detailMap);
        }
	    terrainData.treeInstances = trees.ToArray();
       **/
        return trees;
    }
}
