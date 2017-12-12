using UnityEngine;
using NoiseInterfaces;
using System.Collections.Generic;

public class VegetationGenerator
{
    public VegetationGenerator(){}

    public void PaintGras(long Seed, float[,] Heights, GameObject[] treeObjects, bool[,] streetMap, float WaterLevel, float VegetationMaxHeight, TerrainData terrainData)
    {
        bool[,] TreeMap = new bool[terrainData.heightmapWidth, terrainData.heightmapHeight];
        System.Random prng = new System.Random((int)Seed);
        TreePrototype[] protos = new TreePrototype[treeObjects.Length];
        //TreeInstance[] trees = new TreeInstance[256];
        List<TreeInstance> trees = new List<TreeInstance>(256);
        for (int j = 0; j < treeObjects.Length; j++)
        {
            protos[j] = new TreePrototype
            {
                prefab = treeObjects[j],
                bendFactor = .5f
            };
        }
        int n = 0;
        for (int i = 0; i < 128; i++)
        {
            int x, y;

            x = prng.Next(0, terrainData.heightmapWidth);
            y = prng.Next(0, terrainData.heightmapHeight);
            if (streetMap[x, y] || Heights[y,x] <= WaterLevel || Heights[y,x] > VegetationMaxHeight) continue;
            if (terrainData.GetInterpolatedNormal((float) x / terrainData.heightmapWidth, (float) y / terrainData.heightmapWidth).y < .7f) continue;
            float heightScale = (float)prng.Next(256, 512) / 512.0f;
            trees.Add(new TreeInstance
            {
                prototypeIndex = prng.Next(0, treeObjects.Length - 1),
                position = new Vector3((float)x / terrainData.heightmapWidth,
                        terrainData.GetHeight(x, y) / terrainData.size.y,
                        (float)y / terrainData.heightmapHeight),
                heightScale = heightScale,
                widthScale = heightScale,
                rotation = (float)prng.Next(0, 360) * Mathf.Deg2Rad
            });
            n++;
            TreeMap[x, y] = true;

        }
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
	
	    terrainData.treePrototypes = protos;
	    terrainData.treeInstances = trees.ToArray();
    }
}
