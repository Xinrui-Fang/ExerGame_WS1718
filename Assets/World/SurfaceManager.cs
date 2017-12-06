using UnityEngine;
using NoiseInterfaces;
using HeightMapInterfaces;
using HeightPostProcessors;

public class SurfaceManager : MonoBehaviour {

	void Build(GameObject tile, Vector2Int offset, int tilesize)
	{
		SurfaceCreator creator = tile.GetComponent<SurfaceCreator>();
		Terrain terrain = tile.GetComponent<Terrain>();
		terrain.terrainData = null; // Force the creation of a new terrain!
		
		tile.transform.position = new Vector3(offset.x * tilesize, 0, offset.y * tilesize);
		
		creator.Offset = offset * (creator.Resolution - 1);
		creator.Build();
	}

    void OnEnable() // TODO Maybe even when player moves?
    {
	GameObject root = GameObject.Find("Surface");
	Build(root, new Vector2Int(0, 0), 0);
	
	GameObject left = Instantiate(root);
	Terrain terrain = root.GetComponent<Terrain>();
	
	Build(Instantiate(root), new Vector2Int(-1, 0), (int) terrain.terrainData.size.x);
	Build(Instantiate(root), new Vector2Int(1, 0), (int) terrain.terrainData.size.x);
	Build(Instantiate(root), new Vector2Int(0, 1), (int) terrain.terrainData.size.x);
	Build(Instantiate(root), new Vector2Int(0, -1), (int) terrain.terrainData.size.x);
	
	Build(Instantiate(root), new Vector2Int(-1, -1), (int) terrain.terrainData.size.x);
	Build(Instantiate(root), new Vector2Int(1, 1), (int) terrain.terrainData.size.x);
	Build(Instantiate(root), new Vector2Int(-1, 1), (int) terrain.terrainData.size.x);
	Build(Instantiate(root), new Vector2Int(1, -1), (int) terrain.terrainData.size.x);
    }
}
 
