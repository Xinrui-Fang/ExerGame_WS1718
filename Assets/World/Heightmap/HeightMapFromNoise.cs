using UnityEngine;
using HeightMapInterfaces;
using NoiseInterfaces;

public class HeightMapFromNoise : IScannableHeightSource
{
    private float FeatureFrequency { get; set; }
    public Vector2Int TerrainOffset;
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

    public void ManipulateHeight(ref float[,] heights, int Resolution, int UnitSize)
    {
        float stepSize = 1f / (Resolution -1);
        float val;
        float x_pos, y_pos;
        y_pos = TerrainOffset.y * stepSize;
        for (int y = 0; y < Resolution; y++)
        {
            x_pos = TerrainOffset.x * stepSize;
            for (int x = 0; x < Resolution; x++)
            {
                Iterator.y = y_pos;
                Iterator.x = x_pos;
                val = 0.5f + Noise.Evaluate(Iterator)/2f;
                if (this.PostProcessor == null)
                    heights[x,y] = val;
                else
                    heights[x,y] = PostProcessor.PostProcess(val);
                x_pos += stepSize;
            }
            y_pos += stepSize;
        }
    }

    public float ScanHeight(Vector2 point)
    {
        float val = 0.5f + Noise.Evaluate(point * FeatureFrequency) / 2f;
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
