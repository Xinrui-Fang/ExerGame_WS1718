using HeightMapInterfaces;
using System.Collections.Generic;
using UnityEngine;

public class ComposedHeightMap : IHeightSource
{
    public List<IScannableHeightSource> Sources;
    public List<float> Weights;

    private Vector2 Iterator;
    private IHeightPostProcessor Postprocessor;


    public ComposedHeightMap()
    {
        Sources = new List<IScannableHeightSource>();
        Weights = new List<float>();
        Iterator = new Vector2();
    }

    public ComposedHeightMap(List<IScannableHeightSource> sources, List<float> weights)
    {
        Sources = sources;
        Weights = weights;
    }

    public void AddSource(ref IScannableHeightSource source, float weight)
    {
        this.Sources.Add(source);
        this.Weights.Add(weight);
    }

    public void ManipulateHeight(ref float[,] heights, int Resolution, int UnitSize)
    {
        float stepSize = 1f / Resolution;
        for (int y = 0; y < Resolution; y++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                Iterator.x = x;
                Iterator.y = y;
                Iterator *= stepSize;
                float val = 0;
                for (int i = 0; i < Sources.Count; i++)
                {
                    val += Weights[i] * Sources[i].ScanHeight(Iterator);
                }
                if (Postprocessor == null)
                    heights[x, y] = val;
                else
                    heights[x, y] = Postprocessor.PostProcess(val);
            }
        }
    }

    public void SetPostProcessor(IHeightPostProcessor processor)
    {
        this.Postprocessor = processor;
    }
}