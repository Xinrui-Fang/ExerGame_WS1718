﻿using System.Collections;
using System.Collections.Generic;
using Assets.Utils;
using Assets.World.Heightmap;
using Assets.World.Paths;
using UnityEngine;
using UnityEditor;

/*
this class manage the Player's bicycle : gameplay like a train on rails
*/

public class BicycleV2 : MonoBehaviour {

	public SurfaceManager surfaceManager;
	public float speedMultiplier = 1;
	public float maxSpeed = 5;
	public float maxRotation = 5;
	private Vector3 startPosition ;
	private Vector3 endPosition;
	private List<Vector3> path = new List<Vector3>();
	private int current_node;
	private  List<TerrainChunk> listTerrainChunk;

	// Use this for initialization
	void Start () {
		
		listTerrainChunk = surfaceManager.GetTerrain();
		GameSettings Settings = surfaceManager.Settings;
		// initial terrainChunk 
		Terrain terrain = listTerrainChunk[0].UnityTerrain.GetComponent<Terrain>();
		// Initial Position
		List<NavigationPath> listOfPath = listTerrainChunk[0].GetPathFinder().paths;
		UnityEngine.Debug.Log(string.Format("Path length {0}", listOfPath[0].Waypoints.Count));

		LinkedList<Vector2Int> path2D = listOfPath[0].Waypoints;
		// Le path est dans les coordonnées du TerrainChunk
		// Pour les passer en coordonnées monde -> P_terrain(x, y)*Settings.Size/Settings.HeighMapResolution + Offset_TerrainChunk
		LinkedListNode<Vector2Int>  pathNode = path2D.First;
		while(pathNode != null){
			Vector2Int pathPoint = pathNode.Value;
			float x = pathPoint.x*Settings.Size/Settings.HeightmapResolution + listTerrainChunk[0].GridCoords.x;
			float z = pathPoint.y*Settings.Size/Settings.HeightmapResolution + listTerrainChunk[0].GridCoords.y;
			float y = terrain.SampleHeight(new Vector3(x, 0, z));

			path.Add(new Vector3(x, y, z));
			pathNode = pathNode.Next;
		}
		startPosition = path[0];
		transform.position = startPosition;
	}	
	// Update is called once per frame
	void Update () {
		if(Input.GetKey("up"))
		{
			if ((transform.position - path[current_node]).sqrMagnitude > 1){
				// if the position of the player is not at the path point
				// move until it reach it
				Vector3 pos = Vector3.MoveTowards(transform.position, path[current_node], maxSpeed*Time.deltaTime);
				Vector3 rotation = Vector3.RotateTowards(transform.forward, pos - transform.position, maxRotation*Time.deltaTime, 5);
					
				Quaternion rotationQ = Quaternion.LookRotation(rotation);
				transform.position = pos;
				transform.rotation = rotationQ;
			}else{
				current_node = (current_node +1) % path.Count;
			}
		}
		if(Input.GetKey("down")){
			if(current_node > 0){
				if (transform.position != path[current_node-1]){
					// if the position of the player is not at the path point
					// move until it reach it
					Vector3 pos = Vector3.MoveTowards(transform.position, path[current_node-1], maxSpeed*Time.deltaTime);
					Vector3 rotation = Vector3.RotateTowards(transform.forward, pos - transform.position, maxRotation*Time.deltaTime, 5);
					
					Quaternion rotationQ = Quaternion.LookRotation(rotation);
					transform.position = pos;
					transform.rotation = rotationQ;
				}else{
					current_node = (current_node -1) % path.Count;
				}
			}else{
				if (transform.position != startPosition){
					// if the position of the player is not at the path point
					// move until it reach it
					Vector3 pos = Vector3.MoveTowards(transform.position, startPosition, maxSpeed*Time.deltaTime);
					Vector3 rotation = Vector3.RotateTowards(transform.forward, pos - transform.position, maxRotation*Time.deltaTime, 5);
					
					Quaternion rotationQ = Quaternion.LookRotation(rotation);
					transform.position = pos;
					transform.rotation = rotationQ;
				}
			}
		
		}
	}
}
