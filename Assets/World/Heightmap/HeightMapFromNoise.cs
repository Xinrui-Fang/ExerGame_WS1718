using UnityEngine;
using HeightMapInterfaces;
using NoiseInterfaces;

public class HeightMapFromNoise : IScannableHeightSource
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

    public float ScanHeight(Vector2 position)
    {
        float val = 0.5f + Noise.Evaluate(position * FeatureFrequency + TerrainOffset) / 2f;
        if (this.PostProcessor == null)
           return val;
        else
            return PostProcessor.PostProcess(val);
    }

    public void SetPostProcessor(IHeightPostProcessor processor)
    {
        this.PostProcessor = processor;
    }
}