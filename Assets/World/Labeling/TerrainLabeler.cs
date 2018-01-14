// Inspired by https://alastaira.wordpress.com/2013/11/14/procedural-terrain-splatmapping/

using UnityEngine;
using System.Linq; // used for Sum of array
using Assets.Utils;

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

    public static void MapTerrain(TerrainChunk terrain, float[,] moisture, float[,] Heights, Vector3[,] Normals, float[,,] SplatMap, int[,] streetMap, float WaterLevel, float VegetationMaxHeight, Vector2 TerrainOffset)
    {
        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        CircleBound streetCollider = new CircleBound(new Vector2(), 1f);
        CircleBound treeCollider = new CircleBound(new Vector2(), 1.5f);

        for (int y = 0; y < SplatMap.GetLength(0); y++)
        {

            // Normalise x/y coordinates to range 0-1 
            float y_01 = (float)y / ((float)SplatMap.GetLength(0) - 1);
            float fy_hm = y_01 * (Heights.GetLength(0) - 1);
            int y_hm = Mathf.CeilToInt(fy_hm);

            for (int x = 0; x < SplatMap.GetLength(1); x++)
            {
                float x_01 = (float)x / ((float)SplatMap.GetLength(1) - 1);
                float fx_hm = x_01 * (Heights.GetLength(1) -1);
                int x_hm = Mathf.CeilToInt(fx_hm);
                

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[SplatMap.GetLength(2)];

                streetCollider.Center = terrain.ToWorldCoordinate(x_01, y_01);

                treeCollider.Center = streetCollider.Center;

                Vector3 normal = Normals[y_hm, x_hm];
                float height = Heights[y_hm, x_hm];
                float moist = moisture[y_hm, x_hm];
                if (height <= WaterLevel) // sand
                {
                    splatWeights[3] = 1f;
                }
                else if (normal.y > .7 && height > VegetationMaxHeight) // snow
                {
                    float snowlevel = Mathf.InverseLerp(.7f, 1f, normal.y) * Mathf.InverseLerp(VegetationMaxHeight, 1f, height);
                    splatWeights[1] = snowlevel;
                    splatWeights[2] = (1f - snowlevel) * moist;
                    splatWeights[2] = (1f - snowlevel) * (1f - moist);
                }
                else if (terrain.Objects.Collides(streetCollider, QuadDataType.street)) // street
                {
                    splatWeights[4] = 1f;
                    splatWeights[8] = .5f;
                }
                else if (terrain.Objects.Collides(treeCollider, QuadDataType.vegetation)) // tree soil
                {
                    if (normal.y > .9)
                    {
                        splatWeights[8] = .5f;
                        splatWeights[7] = .5f;
                        splatWeights[0] = .1f;
                    } else
                    {
                        splatWeights[6] = 1f;
                        splatWeights[2] = 1f;
                    }
                }
                else
                {
                    
                    //Vector3 normal = new Vector3();
                    //NormalsFromHeightMap.InterpolateNormal(fx_hm, fy_hm, normal, Normals);
                    
                    float vegetaionLikelihood = Mathf.Sin(Mathf.InverseLerp(WaterLevel, VegetationMaxHeight, height)*Mathf.PI) * moist;
                    float snowCandidate = IsInside(VegetationMaxHeight, 1f, height);
                    float snowLikelikhood = Mathf.Pow(snowCandidate * Mathf.InverseLerp(VegetationMaxHeight, 1f, height), 2) * moist;
                    float slope = Mathf.Pow(1f - Mathf.InverseLerp(.5f, .9f, normal.z), 2);
                    float RockLikelihood = IsInside(WaterLevel + .15f, 1f, height) * Mathf.InverseLerp(WaterLevel * .15f, .9f, height) + IsInside(0f, .8f, normal.y);
                    float SandLikelihood = IsInside(0f, WaterLevel + .15f, height) * (1f - Mathf.InverseLerp(0f, WaterLevel + .15f, height));

                    //0 public Texture2D GrasTexture;
                    //1 public Texture2D SnowTexture;
                    //2 public Texture2D RockTexture;
                    //3 public Texture2D SandTexture;
                    //4 public Texture2D PathTexture;
                    //5 public Texture2D CliffTexture;
                    splatWeights[0] = vegetaionLikelihood * Mathf.InverseLerp(.8f, 1f, normal.y);
                    //splatWeights[0] = moisture[y_hm, x_hm] * Mathf.InverseLerp(.6f, 1f, normal.y) * .2f * Mathf.Pow(Mathf.Clamp(0, .2f, .25f - Mathf.Abs(.25f - height)), 2f);
                    //splatWeights[1] = Mathf.InverseLerp(Mathf.Cos(45f * Mathf.Deg2Rad), 1f, normal.y) * Mathf.Pow(Mathf.InverseLerp(.6f, 1f, height), 2f) * moisture[y_hm, x_hm];
                    splatWeights[1] = snowLikelikhood * Mathf.InverseLerp(.8f, 1f, normal.y);
                    splatWeights[2] = slope * moist * RockLikelihood;
                    //splatWeights[2] = 4 * (1f - Mathf.InverseLerp(Mathf.Cos(60 * Mathf.Deg2Rad), Mathf.Cos(45f * Mathf.Deg2Rad), normal.y));
                    splatWeights[3] = SandLikelihood;
                    //splatWeights[3] = (1f - Mathf.InverseLerp(0f, .5f, normal.y)) * Mathf.Pow(Mathf.InverseLerp(.9f - WaterLevel, 1f, 1f - height), 2f) * (1f - moisture[y_hm, x_hm]);
                    //splatWeights[4] = splatWeights[0] * .7f + splatWeights[3] * .5f * (1f-moisture);
                    splatWeights[5] = slope * (1f - moist) * RockLikelihood;
                }

                //Debug.Log(String.Format("({0}, {1})", x_heightmap, y_heightmap));
                /**
                if (splatWeights[1] > .2f)
                {
                    splatWeights[0] = splatWeights[0] > splatWeights[1] ? splatWeights[0] - splatWeights[1] : 0;
                    splatWeights[2] = splatWeights[2] > splatWeights[1] ? splatWeights[2] - splatWeights[1] : 0;
                    splatWeights[3] = splatWeights[3] > splatWeights[1] ? splatWeights[3] - splatWeights[1] : 0;
                    splatWeights[5] = splatWeights[5] > splatWeights[1] ? splatWeights[5] - splatWeights[1] : 0;
                }
                **/
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
