// Inspired by https://alastaira.wordpress.com/2013/11/14/procedural-terrain-splatmapping/

using UnityEngine;
using System.Linq; // used for Sum of array
using NoiseInterfaces;

public static class TerrainLabeler
{
    public static TerrainData MapTerrain(INoise2DProvider noise, TerrainData terrainData, bool[,] streetMap, float WaterLevel, float VegetationMaxHeight)
    {
        Vector2Int heightmapLimits = new Vector2Int(terrainData.heightmapWidth - 1, terrainData.heightmapWidth - 1);
        Vector2 location = new Vector2();
        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];
        MapTools.VariableDistCircle GetCircleNodes = new MapTools.VariableDistCircle(heightmapLimits, new Vector2Int(0, 0), 1, 1);
        Location2D[] CircleNodes = GetCircleNodes.AllocateArray();

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData.alphamapHeight;
                float x_01 = (float)x / (float)terrainData.alphamapWidth;
                location.x = x_01;
                location.y = x_01;
                int x_heightmap = Mathf.CeilToInt(x_01 * (terrainData.heightmapWidth - 1));
                int y_heightmap = Mathf.CeilToInt(y_01 * (terrainData.heightmapWidth -1));


                bool isStreetMapNeighbour = false;
                float streetNeighbourFactor = 0f;

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];


                if (streetMap[x_heightmap, y_heightmap])
                {

                    splatWeights[4] = 1f;
                }
                else
                {
                    GetCircleNodes.GetNeighbors(x_heightmap, y_heightmap, ref CircleNodes);
                    foreach (var node in CircleNodes)
                    {
                        if (node.valid && streetMap[node.x, node.y])
                        {
                            isStreetMapNeighbour = true;
                            streetNeighbourFactor += .125f / (1f + MapTools.OctileDistance(x_heightmap, y_heightmap, node.x, node.y));
                        }
                    }
                    // Calculate the steepness of the terrain
                    float moisture = .5f + noise.Evaluate(location)/2f;
                    Vector3 normal = terrainData.GetInterpolatedNormal(x_01, y_01);
                    float steepness = Mathf.InverseLerp(0f, terrainData.size[1], terrainData.GetSteepness(y_01, x_01));
                    float slope = steepness * steepness;
                    // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                    // clamp value to 0 - 1 by dividing by the total terrain height in unity units.
                    float height = Mathf.InverseLerp(0f, terrainData.size[1], terrainData.GetHeight(y_heightmap, x_heightmap));

                    //0 public Texture2D GrasTexture;
                    //1 public Texture2D SnowTexture;
                    //2 public Texture2D RockTexture;
                    //3 public Texture2D SandTexture;
                    //4 public Texture2D PathTexture;
                    //5 public Texture2D CliffTexture;
                    splatWeights[0] = moisture*Mathf.Clamp01(normal.y) * .4f * Mathf.Pow(Mathf.Clamp(0, .2f, .25f - Mathf.Abs(.25f - height)), 2f);
                    splatWeights[1] = Mathf.Clamp01(normal.y) * Mathf.Pow(Mathf.InverseLerp(.6f, 1f, height), 2f) * moisture;
                    splatWeights[2] = slope*height*(1f+2f*steepness) * (1f - moisture);
                    splatWeights[3] = steepness * Mathf.Pow(Mathf.InverseLerp(.8f, 1f, 1f - height), 2f) * (1f - moisture);
                    splatWeights[4] = splatWeights[0] * .7f + splatWeights[3] * .5f * (1f-moisture);
                    splatWeights[5] = Mathf.Abs(normal.z) * Mathf.Abs(normal.x) * Mathf.InverseLerp(.2f, .8f, height) * (1f - moisture);
                }
                

                //Debug.Log(String.Format("({0}, {1})", x_heightmap, y_heightmap));
                
                if (isStreetMapNeighbour)
                    splatWeights[4] += 1f + streetNeighbourFactor;
                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();
                // Loop through each terrain texture
                for (int i = 0; i < terrainData.alphamapLayers; i++)
                {

                    // Normalize so that sum of all texture weights = 1
                    splatWeights[i] /= z;

                    // Assign this point to the splatmap array
                    splatmapData[x, y, i] = splatWeights[i];
                }
            }
        }

        // Finally assign the new splatmap to the terrainData:
        terrainData.SetAlphamaps(0, 0, splatmapData);
        return terrainData;
    }
}