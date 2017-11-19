using UnityEngine;
using NoiseInterfaces;

public class VegetationGenerator
{
    public int NumberOfLayers = 3;

    public VegetationGenerator(int NumberOfLayers)
    {
        // TODO: read Number of layers from terrain instead!
        this.NumberOfLayers = NumberOfLayers;
    }

    public TerrainData PaintGras(INoise2DProvider noise, TerrainData terrainData)
    {
        for (int l = 0; l < NumberOfLayers; l++)
        {

            int[,] detailMap = new int[terrainData.detailWidth, terrainData.detailHeight];
            for (int y = 0; y < terrainData.detailHeight; y++)
            {
                float y_01 = (float)y / (float)terrainData.detailHeight;
                for (int x = 0; x < terrainData.detailWidth; x++)
                {
                    float x_01 = (float)x / (float)terrainData.detailWidth;
                    detailMap[x, y] = 0; // (int) (noise.Evaluate(new Vector2(x_01, y_01)) + 1f);
                }
            }
            terrainData.SetDetailLayer(0, 0, l, detailMap);
        }
        return terrainData;
    }
}
