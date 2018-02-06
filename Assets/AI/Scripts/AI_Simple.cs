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
		ActiveTerrain = surfaceManager.GetTile(new Vector2Int(2, 2));
		Model = transform.Find("Model");
		BikeAnimation = Model.GetComponent<Animation>();
		// Initial Position
		WayVertex StartingPoint = ActiveTerrain.GetPathFinder().StartingPoint;
		var longestPath = StartingPoint.GetLongest();
		path = longestPath.path;
		forward = longestPath.forward;
		if (!forward)
		{
			nextNode = path.WorldWaypoints.Length - 1;
		}
		else
		{
			nextNode = 0;
		}
		transform.position = path.WorldWaypoints[nextNode];
		HandleDir = path.WorldWaypoints[RetrieveNext(nextNode)] - path.WorldWaypoints[nextNode];
		HandleDir.Normalize();
		transform.rotation = Quaternion.LookRotation(HandleDir);
	}

	// Update is called once per frame
	void Update()
	{
		int count = 0;
		// If the distance between the player and the next waypoint is less than the distance that can be reached in a unit of time
		// we advance the waypoint
		while ((transform.position - path.WorldWaypoints[nextNode]).magnitude < SkipDist && count < SmoothCount)
		{
			nextNode = RetrieveNext(nextNode);
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
		Leaning = Mathf.MoveTowardsAngle(oldLeaning, (float)System.Math.Round(dirAngle / 90f, 1) * MaxLeaningAngle, LeaningSpeed * Time.deltaTime);
		Leaning = (Leaning + oldLeaning) / 2f;
		Model.rotation *= Quaternion.Euler(-oldLeaning, 0, 0);
		Model.rotation *= Quaternion.Euler(Leaning, 0, 0);

		// Adjust the bike animation speed to the speed of the bike.
		BikeAnimation["Take 001"].speed = Speed * .25f;
	}
}
