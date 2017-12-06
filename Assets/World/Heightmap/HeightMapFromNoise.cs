using UnityEngine;
using HeightMapInterfaces;
using NoiseInterfaces;

public class HeightMapFromNoise : IScannableHeightSource
{
    private float FeatureFrequency { get; set; }
    private Vector2Int TerrainOffset { get; set; }
    public INoise2DProvider Noise;
    private IHeightPostProcessor PostProcessor;
    Vector2 Iterator;

    public HeightMapFromNoise(INoise2DProvider noise, float featureFrequency, Vector2Int TerrainOffset)
    {
        this.Noise = noise;
        this.FeatureFrequency = featureFrequency;
        this.TerrainOffset = TerrainOffset;
        Iterator = new Vector2();
    }

    public float[,] ManipulateHeight(float[,] heights, int Resolution, int UnitSize)
    {
        float stepSize = 1f / Resolution;
        float val;
        for (int y = 0; y < Resolution; y++)
        {
            Iterator.y = y;
            for (int x = 0; x < Resolution; x++)
            {
                Iterator.x = x;
                val = 0.5f + Noise.Evaluate(Iterator * stepSize + TerrainOffset)/2f;
                if (this.PostProcessor == null)
                    heights[x,y] = val;
                else
                    heights[x,y] = PostProcessor.PostProcess(val);
            }
        }
        return heights;
    }

    public float ScanHeight(Vector2 point)
    {
        float val = 0.5f + Noise.Evaluate(point * FeatureFrequency + TerrainOffset) / 2f;
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
