using System.Collections.Generic;
using Assets.World.Paths;
using UnityEngine;

/*
this class manage the Player's bicycle : gameplay like a train on rails
*/

public class AI_Simple : MonoBehaviour
{

    public SurfaceManager surfaceManager;
    public float speedMultiplier = 1;
    public float maxSpeed = 5;
    public float maxRotation = 5;
    private TerrainChunk ActiveTerrain;
    private bool forward;
    private NavigationPath path;
    private int nextNode;

    public void PlaceBike()
    {
        float rayOffset = .1f;
        float rayMaxDist = .2f;
        Ray posRay = new Ray(transform.position + rayOffset * transform.up, -transform.up);
        RaycastHit posHit;
        if (Physics.Raycast(posRay, out posHit, rayMaxDist))
        {
            transform.position = posHit.point;
        }

        Vector3 pos1 = -transform.right * .25f + transform.position;
        Vector3 pos2 = transform.right * .25f + transform.position;
        Ray ray1 = new Ray(pos1 + rayOffset * transform.up, -transform.up);
        Ray ray2 = new Ray(pos2 + rayOffset * transform.up, -transform.up);
        RaycastHit hit1, hit2;
        if (Physics.Raycast(ray1, out hit1, rayMaxDist))
        {
            if (Physics.Raycast(ray2, out hit2, rayMaxDist))
            {
                transform.rotation = Quaternion.LookRotation(hit1.point - hit2.point);
                transform.rotation *= Quaternion.Euler(0, 90, 0);
            }
        }
    }

    private int GetNextPath(WayVertex vertex, int node)
    {
        PathWithDirection dpath = vertex.GetLongest(new PathWithDirection(path, forward));
        if (dpath.path.WorldWaypoints != null)
        {
            //Debug.Log(string.Format("Found Path of lenght {0}", dpath.path.WorldWaypoints.Length));
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
    public void Init()
    {
        ActiveTerrain = surfaceManager.GetTile(new Vector2Int(2,2));
        // Initial Position
        WayVertex StartingPoint = ActiveTerrain.GetPathFinder().StartingPoint;
        var longestPath = StartingPoint.GetLongest();
        path = longestPath.path;
        forward = longestPath.forward;
        if (!forward)
        {
            nextNode = path.WorldWaypoints.Length -1;
        } else
        {
            nextNode = 0;
        }
        transform.position = path.WorldWaypoints[nextNode];
        transform.rotation = Quaternion.LookRotation(path.WorldWaypoints[RetrieveNext(nextNode)] - path.WorldWaypoints[nextNode]);
        transform.rotation *= Quaternion.Euler(0, 90, 0);
    }
    // Update is called once per frame
    void Update()
    {
        int count = 0;
        while ((transform.position - path.WorldWaypoints[nextNode]).sqrMagnitude < 2f * maxSpeed * Time.deltaTime && count < 5)
        {
            nextNode = RetrieveNext(nextNode);
            count++;
        }
        // if the position of the player is not at the path point
        // move until it reach it
        Vector3 pos = Vector3.MoveTowards(transform.position, path.WorldWaypoints[nextNode], maxSpeed * Time.deltaTime);

        Quaternion rotationQ = Quaternion.LookRotation(path.WorldWaypoints[nextNode] - transform.position);
        transform.position = pos;
        transform.rotation = rotationQ;
        transform.rotation *= Quaternion.Euler(0, 90, 0);
        PlaceBike();
    }
}
