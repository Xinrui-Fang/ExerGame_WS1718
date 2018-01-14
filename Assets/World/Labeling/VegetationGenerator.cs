using UnityEngine;
using System.Collections.Generic;
using Assets.Utils;

public class VegetationGenerator
{
    public VegetationGenerator(){}

    public List<TreeInstance> PaintGras(TerrainChunk terrain, long Seed, int[,] streetMap, float WaterLevel, float VegetationMaxHeight, int MaxTreeCount, Vector3[,] Normals)
    {
        GameSettings Settings = terrain.Settings;
        int x_res, y_res;
        x_res = terrain.Heights.GetLength(1);
        y_res = terrain.Heights.GetLength(0);
        float p = ((float) Settings.MaxTreeCount/((float) x_res * y_res));
        if (p >= .5f) p = .25f;

        System.Random prng = new System.Random((int) Seed);

        List<TreeInstance> trees = new List<TreeInstance>(MaxTreeCount);
        CircleBound circ = new CircleBound(new Vector2(), 1.5f);
        for (int y = 0; y < y_res; y++)
        {
            for (int x = 0; x < x_res; x++)
            {
                // Check that we are in Vegetation Height.
                if (terrain.Heights[y, x] <= WaterLevel || terrain.Heights[y, x] > VegetationMaxHeight) continue;

                // Check that terrain is not too steep.
                if (Normals[y, x].y < .8f) continue;

                // Check moisture
                if (terrain.Moisture[y, x] < .3f) continue;

                // Check that we are not colliding with anything.
                Vector2 WorldCoordinates = terrain.ToWorldCoordinate(x, y);
                circ.Center = WorldCoordinates;
                if (terrain.Objects.Collides(circ)) continue;

                // Make a weighted cointoss to decide whether we create a tree.
                if (prng.NextDouble() > p) continue;

                // Create tree
                float heightScale = (float)prng.Next(256, 512) / 512.0f;
                trees.Add(new TreeInstance
                {
                    prototypeIndex = prng.Next(0, GameSettings.TreeProtoTypes.Length - 1),
                    position = new Vector3((x + .5f)/ x_res,
                            0f,
                            (y + .5f) / y_res),
                    heightScale = heightScale,
                    widthScale = heightScale,
                    rotation = (float)prng.Next(0, 360) * Mathf.Deg2Rad
                });

                // Remember tree location
                bool success = terrain.Objects.Put(
                    new QuadTreeData(WorldCoordinates, QuadDataType.vegetation, 0)
                );
                if (!success)
                {
                    Debug.Log(string.Format("Could not add Tree at {0} to QuadTree.", WorldCoordinates));
                }

            }
        }
        /*
        CircleBound smallCirc = new CircleBound(new Vector2(), 1f);
        CircleBound MediumCirc = new CircleBound(new Vector2(), 2f);
        CircleBound BigCirc = new CircleBound(new Vector2(), 3f);
        List<int[,]> DetailMapList = new List<int[,]>(GameSettings.DetailPrototypes.Length);
        float fract = 1f / (float)GameSettings.DetailPrototypes.Length;

        for (int l = 0; l < GameSettings.DetailPrototypes.Length; l++)
        {
            DetailMapList.Add(new int[Settings.DetailResolution, Settings.DetailResolution]);
        }
        for (int y = 0; y < Settings.DetailResolution; y++)
        {
            for (int x = 0; x < Settings.DetailResolution; x++)
            {
                // Do not grow on very steep terrain.
                
                if (Normals[y, x].y < .9f) continue;

                // Gras only grows on good soil.
                if (terrain.Moisture[y, x] > .3f) continue;

                // Check that we are in Vegetation Height.
                if (terrain.Heights[y, x] <= WaterLevel || terrain.Heights[y, x] > VegetationMaxHeight) continue;

                smallCirc.Center = terrain.ToWorldCoordinate((float)x/Settings.DetailResolution, (float)y / Settings.DetailResolution);
                if (terrain.Objects.Collides(smallCirc)) continue;

                MediumCirc.Center = smallCirc.Center;
                BigCirc.Center = smallCirc.Center;
                float vegetaionFactor = 1f;
                if (terrain.Objects.Collides(MediumCirc)) vegetaionFactor *= .5f;
                if (terrain.Objects.Collides(BigCirc)) vegetaionFactor *= .75f;

                int max = Mathf.RoundToInt(terrain.Moisture[y, x] * Normals[y,x].y * Normals[y, x].y * Settings.MaxVegetaionDensity * vegetaionFactor * fract);
                for (int l = 0; l < GameSettings.DetailPrototypes.Length; l++)
                {
                    {
                        DetailMapList[l][y, x] = prng.Next(0, max);
                    }
                }
            }
        }
        terrain.DetailMapList = DetailMapList;
        */
        return trees;
    }
}
