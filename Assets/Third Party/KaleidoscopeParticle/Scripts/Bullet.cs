using UnityEngine;
using System.Collections;

public class Bullet : MonoBehaviour {
	public GameObject prefab;
	public float bombTime = 1f;
	float time = 0;
	// Use this for initialization
	void Start () {

	}

	// Update is called once per frame
	void Update () {
		time += Time.deltaTime;
		if (time > bombTime) {
			GameObject obj = (GameObject)Instantiate (prefab);
			obj.transform.LookAt (Random.onUnitSphere);
			obj.transform.position = transform.position;

			Destroy (this.gameObject);
		}
	}
}
