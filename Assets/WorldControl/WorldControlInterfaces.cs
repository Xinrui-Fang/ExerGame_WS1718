using UnityEngine;
using System.Collections;

public interface IMenu
{
    // This function will be called by the GameManager to return to Main Menu
    void ShowMenu();

    // This funciton will be called by GameManager to show the ScoreBoard;
    void ShowScore();

    // will be called by GameManger to add new Score. Player should be prompted to input his name.
    void AddScore(float Score);
}