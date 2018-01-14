using System.Collections.Generic;
using Assets.World.Paths;
using UnityEngine;

/*
this class manage the Player's bicycle : gameplay like a train on rails
*/

public class BicycleV2 : MonoBehaviour {

	public SurfaceManager surfaceManager;
	public float speedMultiplier = 1;
	public float maxSpeed = 5;
	public float maxRotation = 5;
    private TerrainChunk ActiveTerrain;

    private bool forward;
    private NavigationPath path;
    private int nextNode;

    private int GetNextPath(WayVertex vertex, int node)
    {
        PathWithDirection dpath = vertex.GetLongest(new PathWithDirection(path, forward));
        if (dpath.path.WorldWaypoints != null)
        {
            Debug.Log(string.Format("Found Path of lenght {0}", dpath.path.WorldWaypoints.Length));
            path = dpath.path;
            forward = dpath.forward;
            if (!forward)
            {
                return path.WorldWaypoints.Length - 1;
            }
            else
            {
                return 0;
            }
        }
        forward = !forward;
        return node;
    }

    private int RetrieveNext(int node, bool reverse = false)
    {

        if (forward ^ reverse)
        {
            if (node >= path.WorldWaypoints.Length - 2)
            {
                return GetNextPath(path.End, node);
            }
            return node + 1;
        }
        else
        {
            if (node <= 1)
            {
                return GetNextPath(path.Start, node);
            }
            return node - 1;
        }
    }

    // Use this for initialization
    public void Init() {

        ActiveTerrain = surfaceManager.GetTile(new Vector2Int(2,2));
        // Initial Position
        WayVertex StartingPoint = ActiveTerrain.GetPathFinder().StartingPoint;
        var longestPath = StartingPoint.GetLongest();
        path = longestPath.path;
        forward = longestPath.forward;
        if (!forward)
        {
            nextNode = path.WorldWaypoints.Length;
        }
        else
        {
            nextNode = 0;
        }
        transform.position = path.WorldWaypoints[nextNode];
        transform.rotation = Quaternion.LookRotation(path.WorldWaypoints[RetrieveNext(nextNode)] - path.WorldWaypoints[nextNode]);
        transform.rotation *= Quaternion.Euler(0, 90, 0);
    }	

	// Update is called once per frame
	void Update () {
		if(Input.GetKey("up") || Input.GetKey("down"))
		{
            bool reverse = Input.GetKey("down");
            int count = 0;
            while ((transform.position - path.WorldWaypoints[nextNode]).sqrMagnitude < 2f * maxSpeed * Time.deltaTime && count < 5)
            {
                nextNode = RetrieveNext(nextNode, reverse);
                count++;
            }
            // if the position of the player is not at the path point
            // move until it reach it
            Vector3 pos = Vector3.MoveTowards(transform.position, path.WorldWaypoints[nextNode], maxSpeed*Time.deltaTime);
			Transform copy = transform;
			copy.rotation *= Quaternion.Euler(0, -90, 0);
			Vector3 newDir = Vector3.RotateTowards(copy.forward, path.WorldWaypoints[nextNode] - transform.position, maxRotation*Time.deltaTime, 0.0f);
			Quaternion rotationQ = Quaternion.LookRotation(newDir);
			transform.position = pos;
			transform.rotation = rotationQ;
			transform.rotation *= Quaternion.Euler(0, 90, 0);
		}
	}
}
