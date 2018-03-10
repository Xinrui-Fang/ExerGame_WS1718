using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ScoreMenu : MonoBehaviour {

    private class Score{
        private float value;
        private int duration;

        public Score(float val, int time){
            this.value = val;
            this.duration = time;
        }

        public float getValue(){ return this.value;}
        public void setValue(float val){ this.value = val;}

        public int getDuration(){ return this.duration;}
        public void setDuration(int dur){ this.duration = dur;}

    }

    public Font font;

    private List<float> scores = new List<float>();
    private List<int> durations = new List<int>();
    
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
        float newScore = PlayerPrefs.GetFloat("CurrentScore");
        int newDuration = PlayerPrefs.GetInt("CurrentDuration");
        
        if (newScore != 0){
            
        }
	}
}
