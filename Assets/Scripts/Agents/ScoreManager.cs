using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour 
{
	public int Score = 0;
	public float Multiplier = 10.0f;
	
	private Vector3 LastPosition = new Vector3(); 
	
	void Start()
	{
		LastPosition = gameObject.transform.position;
	}
	
	void Update()
	{
		var diff = LastPosition - gameObject.transform.position;
		LastPosition = gameObject.transform.position;
		
		Score += (int) (diff.magnitude * Multiplier);

        PlayerPrefs.SetFloat("CurrentScore", Score);
        PlayerPrefs.SetInt("CurrentDuration", (int)Time.realtimeSinceStartup);
	}
	
	void OnGUI()
	{
		 GUI.Label(new Rect(25, 25, 100, 30), "Score: " + Score);
	}
}
 
