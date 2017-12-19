using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI_Simple : MonoBehaviour {

	public Transform[] path; //Path to follow
	public float speed; // Speed of the AI ---> Const
	
	public int current_node; // Current position in the Transform[] tab

	// Update is called once per frame
	void Update () {
		if (transform.position != path[current_node].position){
			// if the position of the AI is not at the path point
			// move until it reach it
			Vector3 pos = Vector3.MoveTowards(transform.position, path[current_node].position, speed*Time.deltaTime);
			GetComponent<Rigidbody>().MovePosition(pos);
		}else{
			current_node = (current_node +1) % path.Length;
		}
	}
}
