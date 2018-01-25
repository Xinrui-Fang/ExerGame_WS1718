using UnityEngine;
using System.Collections.Generic;
using Assets.Utils;

public class VegetationGenerator
{
    public int TreeTerraceRadius = 4;
    public int TreeTerraceSmoothRadius = 2;

    public VegetationGenerator(){}

    public List<TreeInstance> PaintGras(TerrainChunk terrain, long Seed, int[,] streetMap, float WaterLevel, float VegetationMaxHeight, int MaxTreeCount, Vector3[,] Normals)
    {
        GameSettings Settings = terrain.Settings;
        int x_res, y_res;
        x_res = terrain.Heights.GetLength(1);
        y_res = terrain.Heights.GetLength(0);
        float p = ((float) Settings.MaxTreeCount/((float) x_res * y_res));
        if (p >= .5f) p = .25f;

        float StreetDistance = (Settings.Size / (float)(Settings.HeightmapResolution -1)) * (TreeTerraceRadius - .5f * (float)TreeTerraceSmoothRadius);
        System.Random prng = new System.Random((int) Seed);

        List<TreeInstance> trees = new List<TreeInstance>(MaxTreeCount);
        CircleBound circ = new CircleBound(new Vector2(), 1.5f);
        CircleBound BigCirc = new CircleBound(new Vector2(), StreetDistance);
        Vector2Int LowerLimits = new Vector2Int(0, 0);

        Vector2Int UpperLimits = new Vector2Int(terrain.Settings.HeightmapResolution - 1, terrain.Settings.HeightmapResolution - 1);
        MapTools.VariableDistCircle VDistCirc = new MapTools.VariableDistCircle(
            LowerLimits, UpperLimits, 1, TreeTerraceRadius
        );
        MapTools.LevelOnCenter Level = new MapTools.LevelOnCenter(VDistCirc, terrain.Heights);
        MapTools.KernelAppliance TerrainSmoother = new MapTools.KernelAppliance(
            new MapTools.VariableDistCircle(LowerLimits, UpperLimits, 1, TreeTerraceRadius),
            new MapTools.VariableDistCircle(LowerLimits, UpperLimits, 1, TreeTerraceSmoothRadius),
            new OctileDistKernel(),
            terrain.Heights
        );
        for (int y = TreeTerraceRadius; y < y_res - TreeTerraceRadius; y++)
        {
            for (int x = TreeTerraceRadius; x < x_res - TreeTerraceRadius; x++)
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
                BigCirc.Center = WorldCoordinates;
                if (terrain.Objects.Collides(circ)) continue;
                if (terrain.Objects.Collides(BigCirc, QuadDataType.street)) continue;

                // Make a weighted cointoss to decide whether we create a tree.
                if (prng.NextDouble() > p) continue;

                // Create tree
                float heightScale = (float)prng.Next(256, 512) / 512.0f;

                Level.Apply(y, x);
                TerrainSmoother.Apply(y, x);
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
                    new QuadTreeData<int>(WorldCoordinates, QuadDataType.vegetation, 0)
                );
                if (!success)
                {
                    Debug.Log(string.Format("Could not add Tree at {0} to QuadTree.", WorldCoordinates));
                }

            }
        }
        return trees;
    }
}
