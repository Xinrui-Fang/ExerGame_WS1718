using UnityEngine;
using NoiseInterfaces;
using HeightMapInterfaces;
using HeightPostProcessors;

public class SurfaceManager : MonoBehaviour {

    public GameSettings Settings;

	TerrainChunk Build(TerrainChunk tile, Vector2Int offset)
	{
        tile.Build(offset);
        return tile;
	}

    void OnEnable() // TODO Maybe even when player moves?
    {
        GameObject DummyTerrainObj = GameObject.Find("Dummy Terrain");
        Terrain DummyTerrain = DummyTerrainObj.GetComponent<Terrain>();
        GameSettings.DetailPrototypes = DummyTerrain.terrainData.detailPrototypes;
        GameSettings.SpatProtoTypes = DummyTerrain.terrainData.splatPrototypes;
        GameSettings.TreeProtoTypes = DummyTerrain.terrainData.treePrototypes;
	    var root = Build(new TerrainChunk(Settings), new Vector2Int(0, 0));
	
	    var S = Build(new TerrainChunk(Settings), new Vector2Int(-1, 0));
	    var N = Build(new TerrainChunk(Settings), new Vector2Int(1, 0));
	    var O = Build(new TerrainChunk(Settings), new Vector2Int(0, 1));
	    var W = Build(new TerrainChunk(Settings), new Vector2Int(0, -1));
	
	    var SW = Build(new TerrainChunk(Settings), new Vector2Int(-1, -1));
	    var NO = Build(new TerrainChunk(Settings), new Vector2Int(1, 1));
	    var SO = Build(new TerrainChunk(Settings), new Vector2Int(-1, 1));
	    var NW = Build(new TerrainChunk(Settings), new Vector2Int(1, -1));

        root.Flush();
        S.Flush();
        N.Flush();
        O.Flush();
        W.Flush();
        SW.Flush();
        NO.Flush();
        SO.Flush();
        NW.Flush();
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
 
