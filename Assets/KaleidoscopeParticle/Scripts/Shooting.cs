using UnityEngine;
using System.Collections;

public class Shooting : MonoBehaviour {
	public GameObject prefab;
	public float randomSize = 5f;
	public float shootSpeed = 1000f;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		if (Input.GetKeyDown (KeyCode.Space)) {
			GameObject obj = (GameObject)Instantiate (prefab);
			obj.transform.position = transform.position;
			obj.transform.LookAt (Random.onUnitSphere*randomSize+Vector3.up);
			Rigidbody rig = obj.GetComponent<Rigidbody> ();
			rig.AddForce (obj.transform.forward * shootSpeed);
			rig.AddTorque (Random.onUnitSphere * 100f);

		}
	}
}
