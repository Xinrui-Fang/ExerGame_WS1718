using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bicycle : MonoBehaviour {

	public WheelCollider frontWheel;
	public WheelCollider backWheel;

	public float speedMultiplier = 10.0f;
	public float maxSpeed = 1000.0f;
	public float downForce = 100.0f;
	
	// Use this for initialization
	void Start () 
	{
		Rigidbody rb = GetComponent<Rigidbody>();
		rb.centerOfMass = new Vector3(0, -20, 0);
	}
	
	// Update is called once per frame
	// TODO Should we use the current approach, i.e. just use a central force and rotation for steering?
	void FixedUpdate()
	{
		Rigidbody rb = GetComponent<Rigidbody>();
		if(Input.GetKey("up"))
		{
			backWheel.brakeTorque = 0.0f;
			backWheel.motorTorque = 20.0f * speedMultiplier;

			//rb.AddRelativeForce(new Vector3(0, 0, speedMultiplier));
		}
		else if(Input.GetKey("down"))
		{
			backWheel.motorTorque = 0.0f;
			backWheel.brakeTorque = 40.0f * speedMultiplier;
			//frontWheel.motorTorque = 0.0f;
			//rb.AddRelativeForce(new Vector3(0, 0, -speedMultiplier));
		}
		else
		{
			backWheel.brakeTorque = 40.0f * speedMultiplier;
			backWheel.motorTorque = 0.0f;
		}
		
		if(Input.GetKey("left"))
		{

			//transform.Rotate(new Vector3(0, -4, 0));
			frontWheel.steerAngle = -10f;
		}
		else if(Input.GetKey("right"))
		{
			//transform.Rotate(new Vector3(0, 4, 0));
			frontWheel.steerAngle = 10f;
		}
		else
		{
			frontWheel.steerAngle = 0.0f;
		}	
     		
		//frontWheel.motorTorque = backWheel.motorTorque;
		//frontWheel.brakeTorque = backWheel.brakeTorque;

		//GetComponent<Rigidbody>().AddForce(Vector3.down*downForce);
		
		Vector3 rotation = transform.rotation.eulerAngles;
		rotation.z = 0;
		transform.rotation = Quaternion.Euler(rotation);
	}
}
