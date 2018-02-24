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
    private PathWithDirection CurrentPath;
	private int nextNode;

	public int ChangingViewOffset = 10;
	// Used for QTE
	// ChoiceEnd : Choices of possibles roads at the end of our road
	// ChoiceStart : Choices of possibles roads at the beginning of our road (if we're going backward)
	public List<PathWithDirection> ChoicesEnd;
	public int QTEChoice = 0;
	public bool QTENeedsChoice = true;
	public bool QTEAtStart;

    public QTESys QTE_Sys;

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

		// we are at the end of the path
	
        pointOfComparison = CurrentPath.path.WorldWaypoints[CurrentPath.path.WorldWaypoints.Length - 1];

         Debug.Log("CHOICE_END BEFORE FILTERING");
        Debug.Log(string.Format("{0} choices found", ChoicesEnd.Count)); 
        Debug.Log("CHOICE_END AFTER FILTERING"); 

        // Filtering of the choices to keep only relevant choices
        ChoicesEnd = ChoicesFiltering(ChoicesEnd, pointOfComparison);
        if (ChoicesEnd.Count == 1)
        {
            dpath = ChoicesEnd[0];
        }
        else
        {
            Debug.Log(string.Format("QTE CHOICE : {0}", QTE_Sys.getReturn()));
            dpath = ChoicesEnd[QTE_Sys.getReturn()];
            QTE_Sys.stop();
        }
		
		Debug.Log(string.Format("Found Path of lenght {0}", dpath.path.WorldWaypoints.Length));


		// Registration/Replacement of the new path 
		CurrentPath = dpath;
        Debug.Log(string.Format("Start : {0}", CurrentPath.path.Start.Pos)); // /!\ Start.Pos and End.Pos are in cluster's coordinate
        Debug.Log(string.Format("End : {0}", CurrentPath.path.End.Pos));

        // Creation of the new set of possibles choices at the beginning and the end of the new path
        
        if (!CurrentPath.forward)
        {
            CurrentPath.reverse();
        }
        ChoicesEnd = CurrentPath.path.End.GetPaths(CurrentPath);
        QTENeedsChoice = true;
        return  1;

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

        Debug.Log(string.Format("Point de comparaison : {0}", pointOfComparison)); 

        Debug.Log(string.Format("num of path BEFORE filtering : {0} ", choices.Count));
        List<PathWithDirection> newChoices = new List<PathWithDirection>(); // List of our new choices
        int currentPathIndex = -1;

        for (int i = 0; i < choices.Count; i++)
        {
            Debug.Log(string.Format("Chemin n°{0}", i));
            Debug.Log(string.Format("Point de départ de ce chemin : {0}", choices[i].path.WorldWaypoints[0]));
            Debug.Log(string.Format("Point d'arrivée de ce chemin : {0}", choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1])); 


            if (MapTools.Aprox(pointOfComparison, choices[i].path.WorldWaypoints[0])
                || MapTools.Aprox(pointOfComparison, choices[i].path.WorldWaypoints[choices[i].path.WorldWaypoints.Length - 1]))
            {

                // the choice is added to our new choices
                newChoices.Add(choices[i]);
                if(choices[i].path.Equals(CurrentPath.path)){
                    Debug.Log("CURRENT PATH FOUND");
                    currentPathIndex = i;
                }
            }
        }
        // If there's only one solution we keep the previous path (turn around), else we remove it
        if (newChoices.Count != 1 && currentPathIndex != -1)
        {
            newChoices.RemoveAt(currentPathIndex);
        }

        Debug.Log(string.Format("{0} choices found", newChoices.Count));
        return newChoices;
    }

	/** Function 
        Name : RetrieveNext
        Semantics : Compute the next node number to know where to move the player
        Parameters : int : current number of node in our path
        Return type : int : the next node number to which the player is heading
     */
	private int RetrieveNext(int node, bool reverse = false)
	{

		// We'll always go from pathPoints[0] to pathPoints[end]
        // The path will be reversed when the player change the forward direction (pressing "space")
        // !reverse -> pathPoints[0] -> pathPoints[end] + UP = pathPoints[0] -> pathPoints[end]

        /*Debug.Log(string.Format("NODE : {0}", node));
        Debug.Log(string.Format("isFinished : {0}", QTE_Sys.isFinished()));
        Debug.Log(string.Format("QTENeedsChoice : {0}", QTENeedsChoice));*/
        // if we are almost at the end of the path --> QTE

        if (node < CurrentPath.path.WorldWaypoints.Length -22 && !QTE_Sys.isFinished()){
            QTE_Sys.stop();
        }
        
        if (node >= CurrentPath.path.WorldWaypoints.Length - 22 && QTE_Sys.isFinished() && QTENeedsChoice)
        {
           // Debug.Log("node >= path.WorldWaypoints.Length - 22 && QTE_Sys.isFinished()");
            //Debug.Log(string.Format("isFinished : {0}", QTE_Sys.isFinished()));
            Vector3 pointOfComparison = CurrentPath.path.WorldWaypoints[CurrentPath.path.WorldWaypoints.Length - 1];
            // Filtering of the choices to keep only relevant choices
            ChoicesEnd = ChoicesFiltering(ChoicesEnd, pointOfComparison);
            if (ChoicesEnd.Count != 1)
            {
                QTE_Sys.QTE_Initialisation(ChoicesEnd.Count - 1, ChoicesEnd, CurrentPath, pointOfComparison);
            }
            QTENeedsChoice = false;

        }
        // if we are at the end of the path
        if (node >= CurrentPath.path.WorldWaypoints.Length - 1)
        {

            Debug.Log("End of the road, normal");
            // we find another path 
            // & we return the number of the next node
            return GetNextPath(node);
        }
        // else we just return the next node number
        return node + 1;
	}

	// Use this for initialization
	public void Init()
	{

        QTENeedsChoice = true;
		ActiveTerrain = surfaceManager.GetTile(new Vector2Int(2, 2)); // cluster in the middle 
		Model = transform.Find("Model");
		BikeAnimation = Model.GetComponent<Animation>();
		// Initial Position
		WayVertex StartingPoint = ActiveTerrain.GetPathFinder().StartingPoint;

		// initially we take the longest path 
		var longestPath = StartingPoint.GetLongest();
		if (!longestPath.forward)
        { // if !forward we reverse the path to make it simplier
            longestPath.reverse();
            CurrentPath = longestPath;
        }

		ChoicesEnd = CurrentPath.path.End.GetPaths(CurrentPath); // storage of all possibles choices from the end of the path
		
		// the player always begin the game at pathPoints[0] -> pathPoints[end]

        nextNode = 0;

		transform.position = CurrentPath.path.WorldWaypoints[nextNode];

		HandleDir = CurrentPath.path.WorldWaypoints[RetrieveNext(nextNode)] - CurrentPath.path.WorldWaypoints[nextNode];
		HandleDir.Normalize();

		// Bike orientation always to the front
		transform.rotation = Quaternion.LookRotation(CurrentPath.path.WorldWaypoints[RetrieveNext(nextNode)] - CurrentPath.path.WorldWaypoints[nextNode]);
	}

	// Update is called once per frame
	void Update()
	{

		if (Input.GetKey("up"))
		{
			int count = 0;
			// If the distance between the player and the next waypoint is less than the distance that can be reached in a unit of time
			// we advance the waypoint
			while ((transform.position - CurrentPath.path.WorldWaypoints[nextNode]).magnitude < SkipDist && count < SmoothCount)
			{
				nextNode = RetrieveNext(nextNode);
				count++;
			}

			Vector3 TargetDir = (CurrentPath.path.WorldWaypoints[nextNode] - transform.position).normalized;

			transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(TargetDir.normalized, transform.up), maxRotation * Time.deltaTime);
			HandleDir = TargetDir - transform.forward;
			// if the position of the player is not at the path point
			// move until it reach it
			float dirAngle = Vector3.SignedAngle(transform.forward, TargetDir, transform.up);
			float dist = (CurrentPath.path.WorldWaypoints[nextNode] - transform.position).magnitude;
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

    void OnGUI(){
        if (Event.current.Equals(Event.KeyboardEvent("space"))){
            {
            CurrentPath.reverse();
            nextNode = CurrentPath.path.WorldWaypoints.Length - nextNode;
            ChoicesEnd = CurrentPath.path.End.GetPaths(CurrentPath);
            QTENeedsChoice = true;
        }
        }
            
    }
}