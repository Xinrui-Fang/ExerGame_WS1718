﻿using UnityEngine;
using HeightMapInterfaces;
using HeightPostProcessors;

[System.Serializable]
public class HeightMapPostProcessor
{
    public enum PostProcessorType
    {
        Exponent, Terrace, Rescale
    }
    public PostProcessorType type;
    public float value1, value2, value3; // Configuration values for the PostProcessors.

    public IHeightPostProcessor ToIHeightPostProcessor()
    {
        if (type == PostProcessorType.Exponent)
        {
            return new ExponentialPostProcessor(value1);
        }
        if (type == PostProcessorType.Rescale)
        {
            return new HeightRescale(value1, value2);
        }
        return new SmoothTerracingPostProcessor(value1, value2, value3);
    }
}

[System.Serializable]
public class HeightmapSetting
{
    public int Octaves;
    public float Weight, Lacunarity, Persistance, FeatureFrequency;
    public Fractal2DNoise.NoiseBase NoiseType;
    public Fractal2DNoise.FractalNoiseType FractalType = Fractal2DNoise.FractalNoiseType.Normal;
    public IHeightPostProcessor PostProcessor;
    public long Seed;
    public HeightMapPostProcessor[] postprocessors;
    public HeightMapFromNoise.HeightMapType HeightmapType = HeightMapFromNoise.HeightMapType.smooth;

    public IScannableHeightSource GetHeightSource(Vector2Int Offset)
    {
        IScannableHeightSource source = new HeightMapFromNoise(
            new Fractal2DNoise(Persistance, Lacunarity, Octaves, Seed, NoiseType, FractalType),
            FeatureFrequency,
            Offset,
            HeightmapType
        );

        if (postprocessors.Length == 1)
        {
            source.SetPostProcessor(postprocessors[0].ToIHeightPostProcessor());

        } else if (postprocessors.Length > 1)
        {
            ComposedPostProcessor composer = new ComposedPostProcessor();
            for (int i = 0; i < postprocessors.Length; i++)
            {
                composer.AddProcessor(postprocessors[i].ToIHeightPostProcessor());
            }
            source.SetPostProcessor(composer);
        }
        return source;
    }
}

[System.Serializable]
public class SplatTexture
{
    public Texture2D NormalMap, Texture;
    public Vector2 TileSize;
    public float Metallic, Smoothness;

    public SplatPrototype ToSplatPrototype()
    {
        return new SplatPrototype()
        {
            texture = Texture,
            normalMap = NormalMap,
            metallic = Metallic,
            smoothness = Smoothness,
            tileSize = TileSize
        };
    }
}

[System.Serializable]
public class  DetailObject
{
    public float BendFactor, NoiseSpread, MaxHeight, MaxWidth, MinHeight, MinWidth;
    public Color DryColor, HealthyColor;
    public GameObject Prototype;
    public Texture2D PrototypeTexture;
    public DetailRenderMode RenderMode;

    public DetailPrototype ToDetailProtoType()
    {
        return new DetailPrototype()
        {
            bendFactor = BendFactor,
            noiseSpread = NoiseSpread,
            maxHeight = MaxHeight,
            maxWidth = MaxWidth,
            minHeight = MinHeight,
            minWidth = MinWidth,
            dryColor = DryColor,
            healthyColor = HealthyColor,
            prototype = Prototype,
            prototypeTexture = PrototypeTexture,
            renderMode = RenderMode
        };
    }
}

[System.Serializable]
public class GameSettings
{
    public GameObject MainObject;
    public GameObject[] AIs;
    public int ChunkMapSize = 32;
    public int MaxTreeCount = 64;
    public float Depth, WaterLevel, VegetationLevel;
    public int HeightmapResolution, DetailResolution, DetailResolutionPerPatch, Size;
    public GameObject[] Trees;
    public HeightmapSetting[] HeightmapLayers;
    public HeightmapSetting Moisture;
    public SplatTexture[] SplatMaps;
    public DetailObject[] TerrainDetails;
    public long WorldSeed;
    public Material TerrainMaterial;

    public static TreePrototype[] TreeProtoTypes;
    public static SplatPrototype[] SpatProtoTypes;
    public static DetailPrototype[] DetailPrototypes;

    public float TreeBillBoardDistance;
    public float TreeRenderDistance;
    public float DetailRenderDistance;
    public float TreePlantOffset;
    public Vector3 TileCorrection;
    public Vector3 TreeCorrection;

    public float MaxVegetaionDensity;

    public void Prepare()
    {
        GetSplat();
        GetTreePrototypes();
        GetDetail();

        float correctionalOffset = .5f / (float)HeightmapResolution; // Put coordinates in the middle of a tile.
        TileCorrection = new Vector3(correctionalOffset * (float)Size, 0, correctionalOffset * (float)Size);
        TreeCorrection = new Vector3(correctionalOffset, -TreePlantOffset, correctionalOffset);
    }

    public SplatPrototype[] GetSplat()
    {
        if (SpatProtoTypes == null)
        {
            SpatProtoTypes = new SplatPrototype[SplatMaps.Length];
            for (int i = 0; i < SplatMaps.Length; i++)
            {
                SpatProtoTypes[i] = SplatMaps[i].ToSplatPrototype();
            }
        }
        return SpatProtoTypes;
    }

    public DetailPrototype[] GetDetail()
    {
        if (DetailPrototypes == null)
        {
            DetailPrototypes = new DetailPrototype[TerrainDetails.Length];
            for (int i = 0; i < TerrainDetails.Length; i++)
            {
                DetailPrototypes[i] = TerrainDetails[i].ToDetailProtoType();
            }
        }
        return DetailPrototypes;
    }

    public IHeightSource GetHeightMapGenerator(Vector2Int Offset)
    {
        if (HeightmapLayers.Length > 1)
        {
            var hGen = new ComposedHeightMap(Offset);
            for (int i = 0; i < HeightmapLayers.Length; i++)
            {
                IScannableHeightSource source = HeightmapLayers[i].GetHeightSource(Offset);
                hGen.AddSource(source , HeightmapLayers[i].Weight);
            }
            return hGen;
        }
        return HeightmapLayers[0].GetHeightSource(Offset);
    }

    public TreePrototype[] GetTreePrototypes()
    {
        if (TreeProtoTypes == null)
        {
            TreeProtoTypes = new TreePrototype[Trees.Length];
            for (int j = 0; j < Trees.Length; j++)
            {
                TreeProtoTypes[j] = new TreePrototype
                {
                    prefab = Trees[j],
                    bendFactor = .5f
                };
            }
        }
        return TreeProtoTypes;
    }
}
