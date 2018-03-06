// Inspired by https://alastaira.wordpress.com/2013/11/14/procedural-terrain-splatmapping/

using UnityEngine;
using System.Linq; // used for Sum of array
using Assets.Utils;

public static class TerrainLabeler
{
	private static float IsInside(float lower, float upper, float value)
	{
		if (value < lower || value > upper)
		{
			return 0;
		}
		return 1;
	}

	public static void MapTerrain(TerrainChunk terrain, float[,] moisture, float[,] Heights, Vector3[,] Normals, float[,,] SplatMap, int[,] streetMap, float WaterLevel, float VegetationMaxHeight, Vector2 TerrainOffset)
	{
		// Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
		CircleBound streetCollider = new CircleBound(new Vector2(), terrain.Settings.StreetRadius);
		CircleBound treeCollider = new CircleBound(new Vector2(), 1.5f);
		int N = SplatMap.GetLength(2) -1;
		int c = Mathf.FloorToInt(Mathf.Pow(N, 1f / 3f));
		float terrainsmoothing = terrain.Settings.SplatMixing;
		UnityEngine.Debug.LogFormat("N: {0}, c: {1}", N, c);
		for (int y = 0; y < SplatMap.GetLength(0); y++)
		{

			// Normalise x/y coordinates to range 0-1 
			float y_01 = (float)y / ((float)SplatMap.GetLength(0) - 1);
			float fy_hm = y_01 * (Heights.GetLength(0) - 1);
			int y_hm = Mathf.CeilToInt(fy_hm);
			for (int x = 0; x < SplatMap.GetLength(1); x++)
			{
				float x_01 = (float)x / ((float)SplatMap.GetLength(1) - 1);
				float fx_hm = x_01 * (Heights.GetLength(1) - 1);
				int x_hm = Mathf.CeilToInt(fx_hm);


				// Setup an array to record the mix of texture weights at this point
				float[] splatWeights = new float[SplatMap.GetLength(2)];

				streetCollider.Center = terrain.ToWorldCoordinate(x_01, y_01);

				treeCollider.Center = streetCollider.Center;
				
				if (terrain.Objects.Collides(streetCollider, QuadDataType.street)) // street
				{
					splatWeights[N] = 1f;
				}

				else
				{
					Vector3 normal = Normals[y_hm, x_hm];
					float height = Heights[y_hm, x_hm];
					//height = Mathf.InverseLerp(WaterLevel, 1f, height);
					float moist = moisture[y_hm, x_hm];
					float steepness = (1f - (normal.y * normal.y));
					steepness = Mathf.InverseLerp(0f, .5f, steepness);

					int dh, dm, ds;
					dh = Mathf.FloorToInt(height * c);
					dh = dh == c ? c - 1 : dh;
					dm = Mathf.FloorToInt(moist * c);
					dm = dm == c ? c - 1 : dm;
					ds = Mathf.FloorToInt(steepness * c);
					ds = ds == c ? c - 1 : ds;
					int id = dh + dm * c + ds * c * c;
					splatWeights[id] = 1f;
					if (x > 1) {
						for (int i = 0; i < N; i++)
						{
							splatWeights[i] += terrainsmoothing * SplatMap[y, x-1, i];
						}
					}
					if (y > 1)
					{
						for (int i = 0; i < N; i++)
						{
							splatWeights[i] += terrainsmoothing * SplatMap[y-1, x, i];
						}
					}
				}

				// Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
				float z = splatWeights.Sum();
				// Loop through each terrain texture
				for (int i = 0; i < SplatMap.GetLength(2); i++)
				{

					// Normalize so that sum of all texture weights = 1
					splatWeights[i] /= z;

					// Assign this point to the splatmap array
					SplatMap[y, x, i] = splatWeights[i];
				}
			}
		}
	}
}
