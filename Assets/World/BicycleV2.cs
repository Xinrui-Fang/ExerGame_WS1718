﻿using System.Collections.Generic;
using Assets.World.Paths;
using UnityEngine;

/*
this class manage the Player's bicycle : gameplay like a train on rails
*/

public class BicycleV2 : MonoBehaviour
{

    public SurfaceManager surfaceManager;
    public float speedMultiplier = 1;
    public float maxSpeed = 5;
    public float maxRotation = 5;
    private TerrainChunk ActiveTerrain;

    private bool forward;
    private NavigationPath path;
    private int nextNode;

    public int ChangingViewOffset = 10;
    // Used for QTE
    // ChoiceEnd : Choices of possibles roads at the end of our road
    // ChoiceStart : Choices of possibles roads at the beginning of our road (if we're going backward)
    public List<PathWithDirection> ChoicesEnd, ChoicesStart;
    public int QTEChoice = 0;
    public bool QTENeedsChoice;
    public bool QTEAtStart;

    public float error = 1f;

    /** Function 
        Name  : GetNextPath
        Semantics : Compute the next player's path and return the first node's number
        Return type : int : the first node's num
        Parameter : int : current node's number to know if our current path is reversed or not
     */
    private int GetNextPath(int node)
    {
        PathWithDirection dpath;

        Vector3 pointOfComparison;

        /*
        Debug.Log("Current path");
        Debug.Log(string.Format("Departure point of this path : {0}", path.WorldWaypoints[0]));
        Debug.Log(string.Format("Arrival point of this path : {0}", path.WorldWaypoints[path.WorldWaypoints.Length - 1]));   
        */

        // if we are at the end of the path
        if (node >= path.WorldWaypoints.Length - 2)
        {
            pointOfComparison = path.WorldWaypoints[path.WorldWaypoints.Length - 1];

            /* Debug.Log("CHOICE_END BEFORE FILTERING");
            Debug.Log(string.Format("{0} choices found", ChoicesEnd.Count)); */
           

            // Filtering of the choices to keep only relevant choices
            ChoicesEnd = ChoicesFiltering(ChoicesEnd, pointOfComparison);


            /* Debug.Log("CHOICE_END AFTER FILTERING");
            Debug.Log(string.Format("{0} choices found", ChoicesEnd.Count)); */

            dpath = ChoicesEnd[QTEChoice];
        }
        else
        // else -> we are at the begining of the path
        {
            pointOfComparison = path.WorldWaypoints[0];

            /* Debug.Log("CHOICE_START BEFORE FILTERING");
            Debug.Log(string.Format("{0} choices found", ChoicesStart.Count)); */

            // Filtering of the choices to keep only relevant choices
            ChoicesStart = ChoicesFiltering(ChoicesStart, pointOfComparison);

            /* Debug.Log("CHOICE_START AFTER FILTERING");
            Debug.Log(string.Format("{0} choices found", ChoicesStart.Count)); */

            dpath = ChoicesStart[QTEChoice];
        }

        Debug.Log(string.Format("Found Path of lenght {0}", dpath.path.WorldWaypoints.Length));
        
        /*Debug.Log(string.Format("Position du joueur : {0}", transform.position));
        
        if (dpath.forward)
        {
            Debug.Log(string.Format("Point de départ de ce chemin : {0}", dpath.path.WorldWaypoints[0]));
            Debug.Log(string.Format("Point d'arrivée de ce chemin : {0}", dpath.path.WorldWaypoints[dpath.path.WorldWaypoints.Length - 1]));
        }
        else
        {
            Debug.Log(string.Format("Point de départ de ce chemin : {0}", dpath.path.WorldWaypoints[dpath.path.WorldWaypoints.Length - 1]));
            Debug.Log(string.Format("Point d'arrivée de ce chemin : {0}", dpath.path.WorldWaypoints[0]));
        }*/
        

        // Registration/Replacement of the new path 
        path = dpath.path;
        Debug.Log(string.Format("Start : {0}", path.Start.Pos)); // /!\ Start.Pos and End.Pos are in cluster's coordinate
        Debug.Log(string.Format("End : {0}", path.End.Pos));

        // Creation of the new set of possibles choices at the beginning and the end of the new path
        ChoicesEnd = path.End.GetPaths(new PathWithDirection(path, forward));
        ChoicesStart = path.Start.GetPaths(new PathWithDirection(path, forward));

        // Registration of the new sens of the path
        forward = dpath.forward;

        int value;
        // Return of the number of the first node of the new path
        if (forward)
        {
            value = 0;
        }
        else
        {
            value = path.WorldWaypoints.Length - 1;
        }

        return value;

    }


/** Function
    Name : ChoicesFiltering
    Semantics : Filter paths choices according to a point of comparison : only paths connected to the point are keeped
                and the previous path is deleted if it's contained in choices && if there're at least one other option
    Parameters : List<PathWithDirection> : list of all the choices of path we have
               : Vector3 : point of comparison for our filter
    Return : List<PathWithDirection> : list of all the choices after filtering
    Post-condition : all PathWithDirection returned must be connected to the pt of comparison (with an error margin)
 */
private List<PathWithDirection> ChoicesFiltering(List<PathWithDirection> choices, Vector3 pointOfComparison)
{
    /* TRAITEMENT Of ChoiceEnd & ChoicesStart */
    // Suppression of paths that doesn't begin or end at the pointOfComparison (with an error margin)

    /* Debug.Log(string.Format("Point de comparaison : {0}", pointOfComparison)); */

    List<PathWithDirection> newChoices = new List<PathWithDirection>(); // List of our new choices
    PathWithDirection previousPath = new PathWithDirection(); // Storage of the current path if it's contained in choices

    for (int i = 0; i < choices.Count; i++)
    {
        /* Debug.Log(string.Format("Chemin n°{0}", i));
        Debug.Log(string.Format("Point de départ de ce chemin : {0}", choices[i].path.WorldWaypoints[0]));
        Debug.Log(string.Format("Point d'arrivée de ce chemin : {0}", choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1])); */


        if ( //If the first point in choices[i] ist the last point of our path
             // X
            (choices[i].path.WorldWaypoints[0].x <= pointOfComparison.x + error &&
            choices[i].path.WorldWaypoints[0].x >= pointOfComparison.x - error &&
            // Y 
            choices[i].path.WorldWaypoints[0].y <= pointOfComparison.y + error &&
            choices[i].path.WorldWaypoints[0].y >= pointOfComparison.y - error &&
            // Z 
            choices[i].path.WorldWaypoints[0].z <= pointOfComparison.z + error &&
            choices[i].path.WorldWaypoints[0].z >= pointOfComparison.z - error) ||

            //Or if the last point in choices[i] ist the last point of our path 
            // X
            (choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1].x <= pointOfComparison.x + error &&
            choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1].x >= pointOfComparison.x - error &&
            // Y 
            choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1].y <= pointOfComparison.y + error &&
            choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1].y >= pointOfComparison.y - error &&
            // Z 
            choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1].z <= pointOfComparison.z + error &&
            choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1].z >= pointOfComparison.z - error))
        {
        
            // the choice is added to our new choices
            newChoices.Add(choices[i]);
            
            // Storage of the previous path to remove it after if needed
            if(choices[i].path == path){
                previousPath = choices[i];
            }
        }
    }
    // If there's only one solution we keep the previous path (turn around), else we remove it
    if ( newChoices.Count != 1){
        newChoices.Remove(previousPath);
    }
    return newChoices;
}

/** Function 
    Name : RetrieveNext
    Semantics : Compute the next node number to know where to move the player
    Parameters : int : current number of node in our path
                 bool : true if Up is pressed, false if down
    Return type : int : the next node number to which the player is heading
 */
private int RetrieveNext(int node, bool reverse = false)
{

    // If we go from pathPoints[0] to pathPoints[end] 
    // forward & !reverse -> pathPoints[0] -> pathPoints[end] + UP = pathPoints[0] -> pathPoints[end]
    // !forward & reverse -> pathPoints[end] -> pathPoints[0] + DOWN = pathPoints[0] -> pathPoints[end]
    if (forward ^ reverse)
    {
        // if we are at the end of the path
        if (node >= path.WorldWaypoints.Length - 2)
        {
            Debug.Log("End of the road, normal");
            // we find another path 
            // & we return the number of the next node
            return GetNextPath(node);
        }
        // else we just return the next node number
        return node + 1;
    }
    else
    {
        // forward & reverse -> pathPoints[0] -> pathPoints[end] + DOWN = pathPoints[end] -> pathPoints[0]
        // !forward & !reverse -> pathPoints[end] -> pathPoints[0] + UP = pathPoints[end] -> pathPoints[0]

        // If we are at the beginning of the path 
        if (node <= 1)
        {
            Debug.Log("End of the road, reverse");
            // we find another path from the beginning of the path
            // & we return the number of the next node
            return GetNextPath(node);
        }
        // else we just return the previous node number
        return node - 1;
    }
}

// Use this for initialization
public void Init()
{

    ActiveTerrain = surfaceManager.GetTile(new Vector2Int(2, 2)); // cluster in the middle 
                                                                  // Initial Position
    WayVertex StartingPoint = ActiveTerrain.GetPathFinder().StartingPoint;

    // initially we take the longest path 
    var longestPath = StartingPoint.GetLongest();
    path = longestPath.path;

    // path direction --> true if the player go pathPoints[0] -> pathPoints[end], else false
    forward = longestPath.forward;

    ChoicesEnd = path.End.GetPaths(longestPath); // storage of all possibles choices from the end of the path
    ChoicesStart = path.Start.GetPaths(); // storage of all possibles choices from the beginning of the path

    // if the player go pathPoints[0] -> pathPoints[end]
    if (forward)
    {
        nextNode = 0;
    }
    else
    // else pathPoints[end] -> pathPoints[0]
    {
        nextNode = path.WorldWaypoints.Length -1;
    }

    transform.position = path.WorldWaypoints[nextNode];

    // Bike orientation always to the front
    transform.rotation = Quaternion.LookRotation(path.WorldWaypoints[RetrieveNext(nextNode)] - path.WorldWaypoints[nextNode]);
    transform.rotation *= Quaternion.Euler(0, 90, 0);
}

// Update is called once per frame
void Update()
{

    if (Input.GetKey("up") || Input.GetKey("down"))
    {
        bool reverse = Input.GetKey("down");
        int count = 0;
        // If the distance between the player and the next waypoint is less than the distance that can be reached in a unit of time
        // we advance the waypoint
        while ((transform.position - path.WorldWaypoints[nextNode]).sqrMagnitude < 2f * maxSpeed * Time.deltaTime && count < 5)
        {
            nextNode = RetrieveNext(nextNode, reverse);
            count++;
        }

        // if the position of the player is not at the path point
        // move until it reach it
        Vector3 pos = Vector3.MoveTowards(transform.position, path.WorldWaypoints[nextNode], maxSpeed * Time.deltaTime);
        Transform copy = transform;
        copy.rotation *= Quaternion.Euler(0, -90, 0);
        Vector3 newDir = Vector3.RotateTowards(copy.forward, path.WorldWaypoints[nextNode] - transform.position, maxRotation * Time.deltaTime, 0.0f);
        Quaternion rotationQ = Quaternion.LookRotation(newDir);

        transform.position = pos;
        transform.rotation = rotationQ;
        transform.rotation *= Quaternion.Euler(0, 90, 0);
    }
}
}