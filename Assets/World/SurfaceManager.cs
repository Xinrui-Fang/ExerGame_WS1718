using UnityEngine;
using System.Threading;

public class SurfaceManager : MonoBehaviour {

	public GameSettings Settings;

	// Contains tiles that need to be finalized on the main thread!
	ConcurrentQueue<TerrainChunk> FinalizationQueue = new ConcurrentQueue<TerrainChunk>();
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
			new Vector2Int(1, -1)
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
		ChunkMap = new TerrainChunk[Settings.ChunkMapSize, Settings.ChunkMapSize];
		Vector2Int playerPos = new Vector2Int((int)Mathf.Floor(Settings.MainObject.transform.position.x) / Settings.Size,
				(int)Mathf.Floor(Settings.MainObject.transform.position.z) / Settings.Size);


		ExtendAt(playerPos);
		// TODO Save in TerrainChunk so we can defer this until it is handled in Update
		// Setting Neighbors will reduce Detail seams.
		// Heightmap seams have to be taken care of separately.
		/** reenable once heightmap seams are gone.
		terrain.SetNeighbors(W, N, O, S);
		S.SetNeighbors(SW, terrain, SO, null);
		N.SetNeighbors(NW, null, NO, terrain);
		O.SetNeighbors(terrain, NO, null, SO);
		W.SetNeighbors(null, NW, terrain, SW);

		SW.SetNeighbors(null, W, S, null);
		NO.SetNeighbors(N, null, null, O);
		SO.SetNeighbors(S, O, null, null);
		NW.SetNeighbors(null, null, N, W);
		**/
	}
	
	TerrainChunk GetTile(Vector2Int pos)
	{
		if(pos.x >= 0 && pos.x < Settings.ChunkMapSize
				&& pos.y >= 0 && pos.y < Settings.ChunkMapSize)
		{
			return ChunkMap[pos.x, pos.y];
		}
		
		return null;
	}
}
 
