using UnityEngine;
using HeightMapInterfaces;
using HeightPostProcessors;
using System;
using System.Text;

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
	private long Seed;
	public HeightMapPostProcessor[] postprocessors;
	public HeightMapFromNoise.HeightMapType HeightmapType = HeightMapFromNoise.HeightMapType.smooth;

	public IScannableHeightSource GetHeightSource(Vector2Int Offset, long WorldSeed, int index)
	{
		int hash = index;
		unchecked
		{
			hash = hash + 3277 * (int)WorldSeed;
		}
		System.Random prng = new System.Random(hash);
		byte[] buf = new byte[8];
		prng.NextBytes(buf);
		Seed = BitConverter.ToInt64(buf, 0);

		IScannableHeightSource source = new HeightMapFromNoise(
			new Fractal2DNoise(Persistance, Lacunarity, Octaves, Seed, NoiseType, FractalType),
			FeatureFrequency,
			Offset,
			HeightmapType
		);

		if (postprocessors.Length == 1)
		{
			source.SetPostProcessor(postprocessors[0].ToIHeightPostProcessor());

		}
		else if (postprocessors.Length > 1)
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
public class MixedHeightMap
{
	public HeightmapSetting BaseMap;
	public float BaseWeight;
	public HeightmapSetting Mixer;
	public HeightmapSetting Component1;
	public HeightmapSetting Component2;

	public IHeightSource GetHeightMapGenerator(Vector2Int Offset, long WorldSeed)
	{
		return new DynamicallyComposedHeightMap(
			Offset,
			BaseMap.GetHeightSource(Offset, WorldSeed, 0),
			Mixer.GetHeightSource(Offset, WorldSeed, 1),
			Component1.GetHeightSource(Offset, WorldSeed, 2),
			Component2.GetHeightSource(Offset, WorldSeed, 3),
			BaseWeight
		);
	}
}

[System.Serializable]
public class ItemSettings
{
	public UnityEngine.GameObject Appearance;
	public float SomeProperty = 1.0f;
}

[System.Serializable]
public class GameSettings
{
	public GameObject MainObject;
	public GameObject[] AIs;
	public ItemSettings[] ItemTypes;
	public int ChunkMapSize = 32;
	public int MaxTreeCount = 64;
	public float Depth, WaterLevel, VegetationLevel;
	public int HeightmapResolution, DetailResolution, DetailResolutionPerPatch, Size;
	public HeightmapSetting[] HeightmapLayers;
	public MixedHeightMap Heightmap;
	public HeightmapSetting Moisture;
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

	public int MaxGrasCount;
	public Color HealthyGrasColor;
	public Color DryGrasColor;

	public Material GrasMaterial;

	public float Gravity, MinJumpSpeed, MaxJumpSpeed;
	public float JumpOffsetY, JumpOffsetX, MinJumpDist, MaxJumpDist;
	public float MinGrasDist = .3f;
	public float GrasFieldSize = 32;

	public float StreetRadius = 1f;
	public float gridElementWidth;
	public int StreetNeighborOffset;
	public float CenterStreetNeighborOffset;

	private static string charset = "qwertzuiopü+asdfghjklöä#yxcvbnm,.<1234567890ß!§$%&/()=?`QWERTZUIOPÜASDFGHJKLÖÄ'*>YXCVBNM;:_|²³{[]}^°@€";

	public static long HashString(string input)
	{
		byte[] hash_out = new byte[8];
		using (var sha = new System.Security.Cryptography.SHA256Managed())
		{
			byte[] textData = System.Text.Encoding.UTF8.GetBytes(input);
			byte[] hash = sha.ComputeHash(textData);
			int i = 0;
			foreach (byte chunk in hash)
			{
				hash_out[i] ^= chunk;
				i = (i + 1) % 8;
			}
			return (long)BitConverter.ToUInt64(hash_out, 0);
		}
	}
	public static string RandomString(int length)
	{
		StringBuilder builder = new StringBuilder();
		for (int i = 0; i < length; i++)
		{

			System.Random prng = new System.Random();
			builder.Append(charset[prng.Next(0, charset.Length)]);
		}
		return builder.ToString();
	}

	public void Prepare()
	{
		string WorldSeedString = PlayerPrefs.GetString("GameSeed", "");
		if (WorldSeedString.Length == 0)
		{
			WorldSeedString = RandomString(64);
			PlayerPrefs.SetString("GameSeed", WorldSeedString);
		}
		WorldSeed = HashString(WorldSeedString);
		// Adjust World to Player Settings.
		string terrainType = PlayerPrefs.GetString("TerrainType", "Mix");
		switch (terrainType)
		{
			case ("Valley"):
				Heightmap.BaseWeight = .8f;
				break;
			case ("Mountains"):
				Heightmap.BaseWeight = .3f;
				break;
			default:
				Heightmap.BaseWeight = .55f;
				break;
		}
		string spikiness = PlayerPrefs.GetString("Spikiness", "Normal");
		switch (spikiness)
		{
			case ("Rigid"):
				Heightmap.Component1.Persistance = .45f;
				Heightmap.Component2.Persistance = .45f;
				break;
			case ("Very Rigid"):
				Heightmap.Component1.Persistance = .5f;
				Heightmap.Component2.Persistance = .5f;
				break;
			case ("Extreme"):
				Heightmap.Component1.Persistance = .55f;
				Heightmap.Component2.Persistance = .55f;
				break;
			case ("Smooth"):
				Heightmap.Component1.Persistance = .35f;
				Heightmap.Component2.Persistance = .35f;
				break;
			default:
				Heightmap.Component1.Persistance = .45f;
				Heightmap.Component2.Persistance = .45f;
				break;
		}
		// Calculate some units that are influenced by settings.
		gridElementWidth = (float)Size / (float)HeightmapResolution;
		StreetNeighborOffset = Mathf.FloorToInt(StreetRadius / gridElementWidth) + 1;
		CenterStreetNeighborOffset = gridElementWidth * StreetNeighborOffset;

		float correctionalOffset = .5f / (float)HeightmapResolution; // Put coordinates in the middle of a tile.
		TileCorrection = new Vector3(correctionalOffset * (float)Size, 0, correctionalOffset * (float)Size);
		TreeCorrection = new Vector3(correctionalOffset, -TreePlantOffset, correctionalOffset);
	}

	public IHeightSource GetHeightMapGenerator(Vector2Int Offset)
	{
		return Heightmap.GetHeightMapGenerator(Offset, WorldSeed);
	}
}
