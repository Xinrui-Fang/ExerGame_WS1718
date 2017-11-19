using UnityEngine;
using HeightMapInterfaces;
using NoiseInterfaces;

public class HeightMapFromNoise : IHeightManipulator
{
    private float FeatureFrequency { get; set; }
    private Vector2Int TerrainOffset { get; set; }
    public INoise2DProvider Noise;
    private IHeightPostProcessor PostProcessor;

    public HeightMapFromNoise(INoise2DProvider noise, float featureFrequency, Vector2Int TerrainOffset)
    {
        this.Noise = noise;
        this.FeatureFrequency = featureFrequency;
        this.TerrainOffset = TerrainOffset;
    }
    public HeightMapFromNoise(INoise2DProvider noise, float featureFrequency, Vector2Int TerrainOffset, IHeightPostProcessor PostProcessor)
    {
        this.Noise = noise;
        this.FeatureFrequency = featureFrequency;
        this.TerrainOffset = TerrainOffset;
        this.PostProcessor = PostProcessor;
    }

    public float[,] ManipulateHeight(float[,] heights, int Resolution, int UnitSize)
    {
        float stepSize = 1f / Resolution;
        float val;
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                val = 0.5f + Noise.Evaluate(new Vector2(x,y) * stepSize * FeatureFrequency + TerrainOffset)/2f;
                if (this.PostProcessor == null)
                    heights[x,y] = val;
                else
                    heights[x,y] = PostProcessor.PostProcess(val);
            }
        }
        return heights;
    }
}