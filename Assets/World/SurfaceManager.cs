using UnityEngine;
using System.Threading;
using Assets.Utils;
using System.Collections;

public class SurfaceManager : MonoBehaviour
{

	public GameSettings Settings;

	// Contains tiles that need to be finalized on the main thread!
	ConcurrentQueue<TerrainChunk> FinalizationQueue = new ConcurrentQueue<TerrainChunk>(17);
	QuadTree<TerrainChunk> Chunks = new QuadTree<TerrainChunk>(new RectangleBound(new Vector2(0, 0), 5));

	int ChunkCount = 0;
	public int JumpsPerFrame = 5;

	void Build(TerrainChunk tile, Vector2Int offset)
	{
		tile.Build(offset);
		FinalizationQueue.Push(tile);
	}

	public void Update()
	{
		var tile = FinalizationQueue.TryPop();
		if (tile != null)
		{
			tile.Flush(this);

			bool success = false;

			StopCoroutine("FlushJumps");
			Chunks = Chunks.PutAndGrow(ref success, tile.GridCoords, 0, tile);

			// TODO: Error checking
			ChunkCount++;
			Assets.Utils.Debug.Log(string.Format("We now have {0} chunks loaded.", ChunkCount));

			if (tile.GridCoords.x == 2 && tile.GridCoords.y == 2)
			{
				if (!success) Assets.Utils.Debug.Log("WTF!!!", LOGLEVEL.ERROR);
				GameObject.Find("Camera").SetActive(false);

				Settings.MainObject.GetComponent<BicycleV2>().Init();

				Settings.MainObject.GetComponent<BicycleV2>().enabled = true;
				Settings.MainObject.SetActive(true);

				foreach (GameObject AI in Settings.AIs)
				{
					AI.GetComponent<AI_Simple>().Init();
					AI.GetComponent<AI_Simple>().enabled = true;
					AI.SetActive(true);
				}
			}

			StartCoroutine("FlushJumps");
		}
	}

	// ATTENTION: NOT THREAD SAFE!
	public void ExtendAt(Vector2Int offset)
	{
		Vector2Int[] directions = new Vector2Int[] {
			new Vector2Int(0, 0),
            ///**
			new Vector2Int(-1, 0),
			new Vector2Int(1, 0),
			new Vector2Int(0, 1),
			new Vector2Int(0, -1),

			new Vector2Int(-1, -1),
			new Vector2Int(1, 1),
			new Vector2Int(-1, 1),
			new Vector2Int(1, -1),

			new Vector2Int(2, 0),
			new Vector2Int(-2, 0),
			new Vector2Int(0, 2),
			new Vector2Int(0, -2),
			//**/
			};

		for (int i = 0; i < directions.Length; i++)
		{
			Vector2Int absolutePos = offset + directions[i];
			//Vector2Int windowPos = absolutePos + WindowOffset;

			// Generate tile, if it does not exist yet
			if (Chunks.Get(absolutePos) == null)
			{
				// swap the commented with the uncommented if some exception is not propagating to the maini thread.
				/*
				TerrainChunk terrain = new TerrainChunk(Settings);
				terrain.Build(absolutePos);
				terrain.Flush(this);
				*/
				ThreadPool.QueueUserWorkItem((object item) => Build((TerrainChunk)item, absolutePos), new TerrainChunk(Settings));
			}
		}
	}
	void OnEnable() // TODO Maybe even when player moves?
	{
		GameObject DummyTerrainObj = GameObject.Find("Terrain Template");
		Terrain DummyTerrain = DummyTerrainObj.GetComponent<Terrain>();
		DummyTerrain.terrainData.SetDetailResolution(64, 64);
		DummyTerrain.terrainData.size = new Vector3(.01f, .01f, .01f);
		DummyTerrain.terrainData.heightmapResolution = 0;
		GameSettings.DetailPrototypes = DummyTerrain.terrainData.detailPrototypes;
		GameSettings.SpatProtoTypes = DummyTerrain.terrainData.splatPrototypes;
		GameSettings.TreeProtoTypes = DummyTerrain.terrainData.treePrototypes;
		Settings.Prepare();

		Vector2Int playerPos = new Vector2Int(2, 2);
		Settings.MainObject.transform.position.Set(2.5f * Settings.Size, Settings.Depth + 10f, 2.5f * Settings.Size);

		ExtendAt(playerPos);
		StartCoroutine("FlushJumps");
	}

	IEnumerator FlushJumps() {
		GameObject ramp = transform.Find("Platform Template").gameObject;
		while(true) {
			int counter = 0;
			foreach (TerrainChunk chunk in Chunks) {
				var JumpList = chunk.JumpList;
				for (int i = chunk.FlushedJumps; i < JumpList.Count; i++)
				{
					var jump = JumpList[i];
					GameObject LittleRamp = GameObject.Instantiate(ramp);
					LittleRamp.transform.rotation = Quaternion.LookRotation(-(jump.LandingPos - jump.Pos));
					LittleRamp.transform.position = jump.Pos + 1f * (jump.LandingPos - jump.Pos).normalized;
					RaycastHit HitInfo;
					if (Physics.Raycast(
							LittleRamp.transform.position + LittleRamp.transform.up * 10f,
							-LittleRamp.transform.up,
							out HitInfo,
							15f,
							1 << 8)
						)
					{
						LittleRamp.transform.position = HitInfo.point;
					}
					LittleRamp.transform.position += LittleRamp.transform.right * -.5f;
					LittleRamp.transform.parent = chunk.UnityTerrain.transform;
					LittleRamp.transform.name = string.Format("Jump {0}", i);
					jump.Ramp = LittleRamp;
					chunk.FlushedJumps++;
					counter++;
					if (counter == JumpsPerFrame)
					{
						counter = 0;
						yield return new WaitForEndOfFrame();
					}
				}
			}
			yield return new WaitForEndOfFrame();
		}
	} 

	public TerrainChunk GetTile(Vector2Int pos)
	{
		var data = Chunks.Get(new Vector2(pos.x, pos.y));
		return (data == null ? null : data.contents);
	}

	public TerrainChunk GetTile(Vector3 pos)
	{
		return GetTile(new Vector2Int((int)Mathf.Floor(pos.x / Settings.Size), (int)Mathf.Floor(pos.z / Settings.Size)));
	}

	public TerrainChunk GetTile(Vector2 pos)
	{
		return GetTile(new Vector2Int((int)Mathf.Floor(pos.x / Settings.Size), (int)Mathf.Floor(pos.y / Settings.Size)));
	}
}

