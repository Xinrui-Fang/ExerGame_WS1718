using UnityEngine;
using System.Threading;
using Assets.Utils;
using System.Collections;

public class SurfaceManager : MonoBehaviour
{

	public GameSettings Settings;

	// Contains tiles that need to be finalized on the main thread!
	ConcurrentQueue<TerrainChunk> FinalizationQueue = new ConcurrentQueue<TerrainChunk>(24);
	QuadTree<TerrainChunk> Chunks = new QuadTree<TerrainChunk>(new RectangleBound(new Vector2(0, 0), 5));

	int ChunkCount = 0;
	public int JumpsPerFrame = 5;
	public Vector3 PlayerPos;

	void Build(TerrainChunk tile, Vector2Int offset)
	{
		tile.Build(offset, PlayerPos);
		FinalizationQueue.Push(tile);
	}

	public void Update()
	{
		PlayerPos = Settings.MainObject.transform.position;
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

			if (tile.GridCoords.x == 0 && tile.GridCoords.y == 0)
			{
				if (!success) Assets.Utils.Debug.Log("WTF!!!", LOGLEVEL.ERROR);
				GameObject.Find("Camera").SetActive(false);

				Settings.MainObject.GetComponent<BikeBase>().Init();
				Settings.MainObject.GetComponent<BikeBase>().enabled = true;
				Settings.MainObject.GetComponent<PlacementCorrection>().enabled = true;
				Settings.MainObject.GetComponent<BikeAnimation>().enabled = true;
				Settings.MainObject.SetActive(true);

				foreach (GameObject AI in Settings.AIs)
				{
					AI.GetComponent<BikeBase>().Init();
					AI.GetComponent<BikeBase>().enabled = true;
					AI.GetComponent<PlacementCorrection>().enabled = true;
					AI.GetComponent<BikeAnimation>().enabled = true;
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

			new Vector2Int(2, 1),
			new Vector2Int(-2, 1),
			new Vector2Int(1, 2),
			new Vector2Int(1, -2),

			new Vector2Int(2, -1),
			new Vector2Int(-2, -1),
			new Vector2Int(-1, 2),
			new Vector2Int(-1, -2),
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
				/**
				TerrainChunk terrain = new TerrainChunk(Settings);
				terrain.Build(absolutePos, Settings.MainObject.transform.position);
				terrain.Flush(this);
				**/
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
		PlayerPrefs.SetString("GameSeed", "");
		Settings.Prepare();

		Vector2Int playerPos = new Vector2Int(0, 0);
		Settings.MainObject.transform.position.Set(2.5f * Settings.Size, Settings.Depth + 10f, 2.5f * Settings.Size);

		ExtendAt(playerPos);
		StartCoroutine("FlushJumps");
	}

	IEnumerator FlushJumps() {
		GameObject ramp = transform.Find("Platform Template").gameObject;
		while(true) {
			int counter = 0;
			foreach (QuadTreeData<TerrainChunk> chunkdata in Chunks) {
				TerrainChunk chunk = chunkdata.contents;
				var JumpList = chunk.JumpList;
				if (JumpList == null)
				{
					yield return new WaitForEndOfFrame();
					continue;
				}
				var terrainData = chunk.UnityTerrain.GetComponent<Terrain>().terrainData;
				for (int i = chunk.FlushedJumps; i < JumpList.Count; i++)
				{
					var jump = JumpList[i];
					var TerrainNormals = terrainData.GetInterpolatedNormal(jump.Pos.x, jump.Pos.y);
					GameObject LittleRamp = GameObject.Instantiate(ramp);
					Vector3 JumpDir = -(jump.LandingPos - jump.Pos);
					float refDot = Vector3.Dot(JumpDir, JumpDir);
					Vector3 Forward = Vector3.forward * (Vector3.Dot(Vector3.forward, JumpDir)/refDot);
					Vector3 Left = Vector3.left * (Vector3.Dot(Vector3.left, JumpDir) / refDot);
					Vector3 LookRot = (Forward + Left).normalized;
					LittleRamp.transform.rotation = Quaternion.LookRotation(LookRot, TerrainNormals);
					LittleRamp.transform.position = jump.Pos;
					RaycastHit HitInfo;
					if (Physics.Raycast(
							LittleRamp.transform.position + TerrainNormals * 10f,
							-Vector3.up,
							out HitInfo,
							15f,
							1 << 8,
							QueryTriggerInteraction.Ignore)
						)
					{
						LittleRamp.transform.position = HitInfo.point;
						LittleRamp.transform.up = HitInfo.normal;
					}
					//LittleRamp.transform.position += LittleRamp.transform.right * -.5f;
					LittleRamp.transform.parent = chunk.UnityTerrain.transform;
					LittleRamp.transform.name = string.Format("Jump {0}", i);
					jump.Ramp = LittleRamp;
					UnityEngine.Debug.DrawLine(jump.Pos, jump.RayTarget, Color.magenta, 1000f, false);
					UnityEngine.Debug.DrawLine(jump.RayTarget, jump.LandingPos, Color.magenta, 1000f, false);
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

