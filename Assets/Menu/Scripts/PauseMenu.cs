using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{

	private bool isPaused = false; //Lets you know if the game is paused or not.
	void Start()
	{

	}

	// Update is called once per frame
	void Update()
	{
		if (Input.GetKeyDown(KeyCode.Escape))
			isPaused = !isPaused;

		if (isPaused)
			Time.timeScale = 0f; //Time stops
		else
			Time.timeScale = 1.0f; //Time is resuming
	}

	void OnGUI()
	{
		if (isPaused)
		{

			// If the button "Continue" is pressed, the game is resuming
			if (GUI.Button(new Rect(Screen.width / 2 - 40, Screen.height / 2 - 20, 80, 40), "Continue"))
			{
				isPaused = false;
			}

			// If the button "Quit" is pressed, the game returns to the MainMenu
			if (GUI.Button(new Rect(Screen.width / 2 - 40, Screen.height / 2 + 40, 80, 40), "Quit"))
			{
				isPaused = false;
				SceneManager.LoadScene("MenuPanel");

			}
		}
	}
}
