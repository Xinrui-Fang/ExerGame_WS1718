using System.Collections.Generic;
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

	public float FrontTireOffset = .5f;
	public float BackTireOffset = .6f;
	public float RayOffset = .3f;
	public float RayMaxDist = .4f;

	public int SmoothCount;
	public float SkipDist, LeaningThreshold, LeaningSpeed, MaxLeaningAngle;
	private float Leaning;
	private Vector3 HandleDir;
	private Transform Model;
	private Animation BikeAnimation;

	[SerializeField]
	private float Speed;
	[Range(0f, 1f)]
	public float Inertia;

	/// <summary>
	/// Corrects the location and orientation of the bike by casting rays from the top of the tires to the ground
	/// and adjusting so the bike does not intersect the terrain.
	/// </summary>
	public void PlaceBike()
	{
		Ray posRay = new Ray(transform.position + RayOffset * transform.up, -transform.up);
		RaycastHit posHit;
		if (Physics.Raycast(posRay, out posHit, RayMaxDist))
		{
			transform.position = posHit.point;
		}

		Vector3 pos1 = transform.forward * BackTireOffset + transform.position;
		Vector3 pos2 = -transform.forward * FrontTireOffset + transform.position;
		Ray ray1 = new Ray(pos1 + RayOffset * transform.up, -transform.up);
		Ray ray2 = new Ray(pos2 + RayOffset * transform.up, -transform.up);
		RaycastHit hit1, hit2;
		if (Physics.Raycast(ray1, out hit1, RayMaxDist))
		{
			if (Physics.Raycast(ray2, out hit2, RayMaxDist))
			{
				transform.rotation = Quaternion.LookRotation(hit1.point - hit2.point);
			}
		}
	}

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
			value = 1;
		}
		else
		{
			value = path.WorldWaypoints.Length - 2;
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


			if (MapTools.Aprox(pointOfComparison, choices[i].path.WorldWaypoints[0])
				|| MapTools.Aprox(pointOfComparison, choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1]))
			{

				// the choice is added to our new choices
				newChoices.Add(choices[i]);

				// Storage of the previous path to remove it after if needed
				if (choices[i].path == path)
				{
					previousPath = choices[i];
				}
			}
		}
		// If there's only one solution we keep the previous path (turn around), else we remove it
		if (newChoices.Count != 1)
		{
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
			if (node >= path.WorldWaypoints.Length - 1)
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
			if (node <= 0)
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
		Model = transform.Find("Model");
		BikeAnimation = Model.GetComponent<Animation>();
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
			nextNode = path.WorldWaypoints.Length - 1;
		}

		transform.position = path.WorldWaypoints[nextNode];

		HandleDir = path.WorldWaypoints[RetrieveNext(nextNode)] - path.WorldWaypoints[nextNode];
		HandleDir.Normalize();

		// Bike orientation always to the front
		transform.rotation = Quaternion.LookRotation(path.WorldWaypoints[RetrieveNext(nextNode)] - path.WorldWaypoints[nextNode]);
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
			while ((transform.position - path.WorldWaypoints[nextNode]).magnitude < SkipDist && count < SmoothCount)
			{
				nextNode = RetrieveNext(nextNode, reverse);
				count++;
			}

			Vector3 TargetDir = (path.WorldWaypoints[nextNode] - transform.position).normalized;

			transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(TargetDir.normalized, transform.up), maxRotation * Time.deltaTime);
			HandleDir = TargetDir - transform.forward;
			// if the position of the player is not at the path point
			// move until it reach it
			float dirAngle = Vector3.SignedAngle(transform.forward, TargetDir, transform.up);
			float dist = (path.WorldWaypoints[nextNode] - transform.position).magnitude;
			float oldSpeed = Speed;
			Speed = maxSpeed * .5f * (1f - transform.forward.y) * Mathf.Cos(Mathf.Abs(dirAngle * Mathf.Deg2Rad));

			// calculate inertia independant of fps
			float TimedReverseInertia = (1f - Inertia) * Time.deltaTime;
			TimedReverseInertia = TimedReverseInertia <= 0 ? .01f : TimedReverseInertia;
			Speed = (1f - TimedReverseInertia) * oldSpeed + TimedReverseInertia * Speed;

			// do not overshoot the target node.
			dist = Mathf.Min(dist, Speed * Time.deltaTime);
			transform.position = transform.position + transform.forward * dist;

			PlaceBike();
			
			if (Mathf.Abs(dirAngle) < LeaningThreshold) dirAngle = 0;
			float oldLeaning = Leaning;
			Leaning = Mathf.MoveTowardsAngle(oldLeaning, (float) System.Math.Round(dirAngle/90f,1) * MaxLeaningAngle, LeaningSpeed * Time.deltaTime);
			Leaning = (Leaning + oldLeaning) / 2f;
			Model.rotation *= Quaternion.Euler(-oldLeaning, 0, 0);
			Model.rotation *= Quaternion.Euler(Leaning, 0, 0);
		}
		else {
			Speed = 0;
		}
		// Adjust the bike animation speed to the speed of the bike.
		BikeAnimation["Take 001"].speed = Speed * .25f;
	}
}