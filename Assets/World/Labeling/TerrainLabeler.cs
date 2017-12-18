﻿// Inspired by https://alastaira.wordpress.com/2013/11/14/procedural-terrain-splatmapping/

using UnityEngine;
using System.Linq; // used for Sum of array
using Assets.World.Heightmap;

public static class TerrainLabeler
{
    private static float IsInside(float lower, float upper, float value)
    {
        if (value < lower || value > upper)
        {
            return 0;
        }
        return 1;
    }

    public static void MapTerrain(float[,] moisture, float[,] Heights, Vector3[,] Normals, float[,,] SplatMap, int[,] streetMap, float WaterLevel, float VegetationMaxHeight, Vector2 TerrainOffset)
    {
        Vector2Int heightmapLimits = new Vector2Int(Heights.GetLength(0) -1, Heights.GetLength(1) -1);
        Vector2 location = new Vector2();
        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        MapTools.VariableDistCircle GetCircleNodes = new MapTools.VariableDistCircle(heightmapLimits, new Vector2Int(0, 0), 1, 1);
        Location2D[] CircleNodes = GetCircleNodes.AllocateArray();

        for (int y = 0; y < SplatMap.GetLength(0); y++)
        {
            for (int x = 0; x < SplatMap.GetLength(1); x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)SplatMap.GetLength(0);
                float x_01 = (float)x / (float)SplatMap.GetLength(1);
                float fx_hm = x_01 * (Heights.GetLength(1) -1);
                float fy_hm = y_01 * (Heights.GetLength(0) -1);
                int x_hm = Mathf.CeilToInt(fx_hm);
                int y_hm = Mathf.CeilToInt(fy_hm);

                location.x = x_01 * fx_hm;
                location.y = y_01 * fy_hm;


                bool isStreetMapNeighbour = false;
                float streetNeighbourFactor = 0f;

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[SplatMap.GetLength(2)];


                if (streetMap[x_hm, y_hm] > 0)
                {

                    splatWeights[4] = 1f;
                }
                else
                {
                    GetCircleNodes.GetNeighbors(x_hm, y_hm, ref CircleNodes);
                    foreach (var node in CircleNodes)
                    {
                        if (node.valid && streetMap[node.x, node.y] > 0)
                        {
                            isStreetMapNeighbour = true;
                            streetNeighbourFactor += .125f / (1f + MapTools.OctileDistance(x_hm, y_hm, node.x, node.y));
                        }
                    }
                    //Vector3 normal = new Vector3();
                    //NormalsFromHeightMap.InterpolateNormal(fx_hm, fy_hm, normal, Normals);
                    Vector3 normal = Normals[y_hm, x_hm];
                    float height = Heights[y_hm, x_hm];
                    float moist = moisture[y_hm, x_hm];
                    
                    float vegetaionLikelihood = Mathf.Sin(Mathf.InverseLerp(WaterLevel, VegetationMaxHeight, height)*Mathf.PI) * moist;
                    float snowCandidate = IsInside(VegetationMaxHeight, 1f, height);
                    float snowLikelikhood = Mathf.Pow(snowCandidate * Mathf.InverseLerp(VegetationMaxHeight, 1f, height), 2) * moist;
                    float slope = 1f - Mathf.Pow(normal.z, 2);
                    float RockLikelihood = IsInside(WaterLevel + .15f, .9f, height) * Mathf.InverseLerp(WaterLevel * .15f, .9f, height);
                    float SandLikelihood = IsInside(0f, WaterLevel + .15f, height) * (1f - Mathf.InverseLerp(0f, WaterLevel + .15f, height));

                    //0 public Texture2D GrasTexture;
                    //1 public Texture2D SnowTexture;
                    //2 public Texture2D RockTexture;
                    //3 public Texture2D SandTexture;
                    //4 public Texture2D PathTexture;
                    //5 public Texture2D CliffTexture;
                    splatWeights[0] = vegetaionLikelihood * normal.y;
                    //splatWeights[0] = moisture[y_hm, x_hm] * Mathf.InverseLerp(.6f, 1f, normal.y) * .2f * Mathf.Pow(Mathf.Clamp(0, .2f, .25f - Mathf.Abs(.25f - height)), 2f);
                    //splatWeights[1] = Mathf.InverseLerp(Mathf.Cos(45f * Mathf.Deg2Rad), 1f, normal.y) * Mathf.Pow(Mathf.InverseLerp(.6f, 1f, height), 2f) * moisture[y_hm, x_hm];
                    splatWeights[1] = snowLikelikhood * normal.y;
                    splatWeights[2] = slope * moist * RockLikelihood;
                    //splatWeights[2] = 4 * (1f - Mathf.InverseLerp(Mathf.Cos(60 * Mathf.Deg2Rad), Mathf.Cos(45f * Mathf.Deg2Rad), normal.y));
                    splatWeights[3] = SandLikelihood;
                    //splatWeights[3] = (1f - Mathf.InverseLerp(0f, .5f, normal.y)) * Mathf.Pow(Mathf.InverseLerp(.9f - WaterLevel, 1f, 1f - height), 2f) * (1f - moisture[y_hm, x_hm]);
                    //splatWeights[4] = splatWeights[0] * .7f + splatWeights[3] * .5f * (1f-moisture);
                    splatWeights[5] = slope * (1f - moist) * RockLikelihood;
                }

                //Debug.Log(String.Format("({0}, {1})", x_heightmap, y_heightmap));
                
                if (isStreetMapNeighbour)
                    splatWeights[4] += 1f + streetNeighbourFactor;
                else if (splatWeights[1] > .2f)
                {
                    splatWeights[0] = splatWeights[0] > splatWeights[1] ? splatWeights[0] - splatWeights[1] : 0;
                    splatWeights[2] = splatWeights[2] > splatWeights[1] ? splatWeights[2] - splatWeights[1] : 0;
                    splatWeights[3] = splatWeights[3] > splatWeights[1] ? splatWeights[3] - splatWeights[1] : 0;
                    splatWeights[5] = splatWeights[5] > splatWeights[1] ? splatWeights[5] - splatWeights[1] : 0;
                }
                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();
                // Loop through each terrain texture
                for (int i = 0; i < SplatMap.GetLength(2); i++)
                {

                    // Normalize so that sum of all texture weights = 1
                    splatWeights[i] /= z;

                    // Assign this point to the splatmap array
                    SplatMap[y, x, i] = splatWeights[i];
                }
            }
        }
    }
}