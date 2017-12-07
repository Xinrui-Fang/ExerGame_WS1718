using UnityEngine;
using NoiseInterfaces;
using HeightMapInterfaces;
using HeightPostProcessors;

public class SurfaceManager : MonoBehaviour {

	Terrain Build(GameObject tile, Vector2Int offset, int tilesize)
	{
		SurfaceCreator creator = tile.GetComponent<SurfaceCreator>();
		Terrain terrain = tile.GetComponent<Terrain>();
		terrain.terrainData = null; // Force the creation of a new terrain!
		
		tile.transform.position = new Vector3(offset.y * tilesize, 0, offset.x * tilesize);
		
		creator.Offset = offset * (creator.Resolution -1);
		creator.Build();
        return tile.GetComponent<Terrain>();
	}

    void OnEnable() // TODO Maybe even when player moves?
    {
	    GameObject root = GameObject.Find("Surface");
	    Build(root, new Vector2Int(0, 0), 0);
	
	    //GameObject left = Instantiate(root);
	    Terrain terrain = root.GetComponent<Terrain>();
	
	    var S = Build(Instantiate(root), new Vector2Int(-1, 0), (int) terrain.terrainData.size.x);
	    var N = Build(Instantiate(root), new Vector2Int(1, 0), (int) terrain.terrainData.size.x);
	    var O = Build(Instantiate(root), new Vector2Int(0, 1), (int) terrain.terrainData.size.x);
	    var W = Build(Instantiate(root), new Vector2Int(0, -1), (int) terrain.terrainData.size.x);
	
	    var SW = Build(Instantiate(root), new Vector2Int(-1, -1), (int) terrain.terrainData.size.x);
	    var NO = Build(Instantiate(root), new Vector2Int(1, 1), (int) terrain.terrainData.size.x);
	    var SO = Build(Instantiate(root), new Vector2Int(-1, 1), (int) terrain.terrainData.size.x);
	    var NW = Build(Instantiate(root), new Vector2Int(1, -1), (int) terrain.terrainData.size.x);

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
}
 
