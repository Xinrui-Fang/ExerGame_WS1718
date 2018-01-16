using UnityEngine;
using Assets.Utils;
using System.Collections.Generic;

public class GrasField
{
    private GameObject GrasFieldObject;
    private TerrainChunk Terrain;
    public float Spread = 1f;
    float probeDelta = .5f;

    public GrasField(TerrainChunk Terrain, int seed)
    {
        float spreadHalf = Spread / 2f;

        Terrain UnityTerrain = Terrain.UnityTerrain.GetComponent<Terrain>();
        TerrainData UnityTerrainData = UnityTerrain.terrainData;

        GrasFieldObject = new GameObject(
            "GrasField"
        );
        GrasFieldObject.transform.SetParent(Terrain.UnityTerrain.transform);
        GrasFieldObject.AddComponent<MeshFilter>();
        GrasFieldObject.AddComponent<MeshRenderer>();

        MeshFilter Filter = GrasFieldObject.GetComponent<MeshFilter>();

        float[,] heights = Terrain.Heights;
        float[,] moisture = Terrain.Moisture;
        Vector3[,] TerrainNormals = Terrain.Normals;

        int Resolution = Terrain.Heights.GetLength(0);
        int grasCount = Terrain.Settings.MaxGrasCount;
        List<Vector3> positions = new List<Vector3>(grasCount);
        List<Vector3> normals = new List<Vector3>(grasCount);
        List<Vector2> DetailLayer = new List<Vector2>(grasCount);
        List<Color> colors = new List<Color>(grasCount);
        int[] indices = new int[grasCount];

        Color HealtyColor = Terrain.Settings.HealthyGrasColor;
        Color DryColor = Terrain.Settings.DryGrasColor;
        float WaterLevel = Terrain.Settings.WaterLevel;
        float MaxVegLevel = Terrain.Settings.VegetationLevel;

        System.Random prng = new System.Random(seed);

        CircleBound SmallCircle = new CircleBound(new Vector2(), 1.5f);
        CircleBound BigCircle = new CircleBound(new Vector2(), 2f);
        CircleBound LargeCircle = new CircleBound(new Vector2(), 3f);
        Vector2 flatCoord = new Vector2();
        int i = 0;
        int n = 0;
        float ToTerrainOffset = (float) 1f / Terrain.Settings.HeightmapResolution;
        while (i < grasCount && n < grasCount * 5)
        {
            n++;
            float fx = (float)prng.NextDouble();
            float fy = (float)prng.NextDouble();
            int x = Mathf.RoundToInt(fx * (Resolution -1));
            int z = Mathf.RoundToInt(fy * (Resolution - 1));
            Terrain.ToWorldCoordinate(fx, fy, ref flatCoord);
            SmallCircle.Center = flatCoord;
            BigCircle.Center = flatCoord;
            //if (Terrain.Objects.Collides(BigCircle, QuadDataType.street)) continue;
            if (Terrain.Objects.Collides(SmallCircle)) continue;
            float height = heights[z, x];
            if (height < WaterLevel || height > MaxVegLevel) continue;
            if (TerrainNormals[z, x].y < .85) continue;
            if (moisture[z, x] < .3f) continue;
            float health = (Mathf.InverseLerp(.3f, 1f, moisture[z, x]) + Mathf.InverseLerp(0.85f, 1f, TerrainNormals[z, x].y)) / 2f;
            LargeCircle.Center = flatCoord;
            if (Terrain.Objects.Collides(LargeCircle, QuadDataType.vegetation)) health *= .75f;
            if (health < .3f) continue;
            Color healthColor = Color.Lerp(DryColor, HealtyColor, health * health);
            float xd = -spreadHalf + Spread * (float)prng.NextDouble();
            float yd = -spreadHalf + Spread * (float)prng.NextDouble();
            float MinHeight = UnityTerrainData.GetInterpolatedHeight(fx + xd * ToTerrainOffset, fy + yd * ToTerrainOffset);
            if (TerrainNormals[z, x].y < .9f) // on steep terrain seek grass correction
            {
                float HeightProbe = UnityTerrainData.GetInterpolatedHeight(fx + (xd + probeDelta) * ToTerrainOffset, fy + (yd + probeDelta) * ToTerrainOffset);
                MinHeight = MinHeight > HeightProbe ? HeightProbe : MinHeight;
                HeightProbe = UnityTerrainData.GetInterpolatedHeight(fx + (xd - probeDelta) * ToTerrainOffset, fy + (yd - probeDelta) * ToTerrainOffset);
                MinHeight = MinHeight > HeightProbe ? HeightProbe : MinHeight;
                HeightProbe = UnityTerrainData.GetInterpolatedHeight(fx + (xd - probeDelta) * ToTerrainOffset, fy + (yd + probeDelta) * ToTerrainOffset);
                MinHeight = MinHeight > HeightProbe ? HeightProbe : MinHeight;
                HeightProbe = UnityTerrainData.GetInterpolatedHeight(fx + (xd + probeDelta) * ToTerrainOffset, fy + (yd - probeDelta) * ToTerrainOffset);
                MinHeight = MinHeight > HeightProbe ? HeightProbe : MinHeight;
            }

            positions.Add( new Vector3(
                flatCoord.x + xd,
                MinHeight, 
                flatCoord.y + yd)
            );
            DetailLayer.Add(new Vector2(Mathf.Round((float)prng.NextDouble()), .5f * (health + (float) prng.NextDouble())));
            colors.Add(healthColor);
            indices[i] = i;
            Vector3 GrasNormal = new Vector3(0, 1f, 0);
            GrasNormal += UnityTerrainData.GetInterpolatedNormal(x + xd * ToTerrainOffset, fy + yd * ToTerrainOffset);
            GrasNormal.Normalize();
            normals.Add(GrasNormal);//UnityTerrainData.GetInterpolatedNormal(x + xd * ToTerrainOffset, fy + yd * ToTerrainOffset));
            i++;
        }

        Mesh mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetUVs(2, DetailLayer);
        mesh.SetColors(colors);
        mesh.SetNormals(normals);
        mesh.SetIndices(indices, MeshTopology.Points, 0);
        Filter.mesh = mesh;
        MeshRenderer renderer = GrasFieldObject.GetComponent<MeshRenderer>();
        renderer.material = Terrain.Settings.GrasMaterial;
    }
}
