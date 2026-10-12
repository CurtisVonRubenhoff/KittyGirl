using UnityEngine;
using System.Collections;

public class AutoDestroy : MonoBehaviour {
	public float destroyTime = 5.0f;
	float time = 0;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		time += Time.deltaTime;
		if (time > destroyTime) {
			Destroy (this.gameObject);
		}
	}
}
