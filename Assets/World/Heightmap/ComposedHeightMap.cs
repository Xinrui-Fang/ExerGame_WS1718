using HeightMapInterfaces;
using System.Collections.Generic;
using UnityEngine;

public class ComposedHeightMap : IHeightSource
{
    public List<IScannableHeightSource> Sources;
    public List<float> Weights;

    private IHeightPostProcessor Postprocessor;


    public ComposedHeightMap()
    {
        Sources = new List<IScannableHeightSource>();
        Weights = new List<float>();
    }

    public ComposedHeightMap(List<IScannableHeightSource> sources, List<float> weights)
    {
        Sources = sources;
        Weights = weights;
    }

    public void AddSource(IScannableHeightSource source, float weight)
    {
        this.Sources.Add(source);
        this.Weights.Add(weight);
    }

    public float[,] ManipulateHeight(float[,] heights, int Resolution, int UnitSize)
    {
        float stepSize = 1f / Resolution;
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float val = 0;
                for (int i = 0; i < Sources.Count; i++)
                {
                    val += Weights[i] * Sources[i].ScanHeight(new Vector2(x, y) * stepSize);
                }
                if (Postprocessor == null)
                    heights[x, y] = val;
                else
                    heights[x, y] = Postprocessor.PostProcess(val);
            }
        }
        return heights;
    }

    public void SetPostProcessor(IHeightPostProcessor processor)
    {
        this.Postprocessor = processor;
    }
}