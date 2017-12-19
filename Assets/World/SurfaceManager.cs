using UnityEngine;
using System.Threading;

public class SurfaceManager : MonoBehaviour {

	public GameSettings Settings;

	// Contains tiles that need to be finalized on the main thread!
	ConcurrentQueue<TerrainChunk> FinalizationQueue = new ConcurrentQueue<TerrainChunk>(17);
	TerrainChunk[,] ChunkMap = null;
	//Vector2Int WindowOffset = new Vector2Int();
	
	int ChunkCount = 0;
	void Build(TerrainChunk tile, Vector2Int offset)
	{
		tile.Build(offset);
		FinalizationQueue.Push(tile);
	}

	public void Update()
	{
		var tile = FinalizationQueue.TryPop();
		if(tile != null)
		{
			tile.Flush(this);
			ChunkMap[tile.GridCoords.x, tile.GridCoords.y] = tile;
			
			ChunkCount++;
			Debug.Log(string.Format("We now have {0} chunks loaded.", ChunkCount));

            if (tile.GridCoords.x == 2 && tile.GridCoords.y == 2)
            {
                // TODO: enable player and ai.
            }
		}
	}
	
	// ATTENTION: NOT THREAD SAFE!
	public void ExtendAt(Vector2Int offset)
	{
		Vector2Int[] directions = new Vector2Int[] {
			new Vector2Int(0, 0),

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
        };
		
		for(int i = 0; i < directions.Length; i++)
		{
			Vector2Int absolutePos = offset + directions[i];
			//Vector2Int windowPos = absolutePos + WindowOffset;
			
			// Generate tile, if it does not exist yet
			if(absolutePos.x >= 0 && absolutePos.x < Settings.ChunkMapSize
				&& absolutePos.y >= 0 && absolutePos.y < Settings.ChunkMapSize
				&& ChunkMap[absolutePos.x, absolutePos.y] == null)
			{
				ThreadPool.QueueUserWorkItem((object item) => Build((TerrainChunk) item, absolutePos), new TerrainChunk(Settings));
			}
		}
	}
    void OnEnable() // TODO Maybe even when player moves?
    {
        GameObject DummyTerrainObj = GameObject.Find("Dummy Terrain");
        Terrain DummyTerrain = DummyTerrainObj.GetComponent<Terrain>();
        GameSettings.DetailPrototypes = DummyTerrain.terrainData.detailPrototypes;
        GameSettings.SpatProtoTypes = DummyTerrain.terrainData.splatPrototypes;
        GameSettings.TreeProtoTypes = DummyTerrain.terrainData.treePrototypes;
		ChunkMap = new TerrainChunk[Settings.ChunkMapSize, Settings.ChunkMapSize];
        Vector2Int playerPos = new Vector2Int(2, 2);
        Settings.MainObject.transform.position.Set(2.5f * Settings.Size, Settings.Depth + 10f, 2.5f * Settings.Size);
        
		ExtendAt(playerPos);
	}
	
	public TerrainChunk GetTile(Vector2Int pos)
	{
		if(pos.x >= 0 && pos.x < Settings.ChunkMapSize
				&& pos.y >= 0 && pos.y < Settings.ChunkMapSize)
		{
            if (ChunkMap[pos.x, pos.y] == null) return null;
			return ChunkMap[pos.x, pos.y];
		}
		
		return null;
	}
}
 
