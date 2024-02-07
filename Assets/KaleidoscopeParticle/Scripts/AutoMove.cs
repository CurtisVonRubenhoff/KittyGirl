using UnityEngine;
using System.Collections;

public class AutoMove : MonoBehaviour {
	public Vector3 moveSpeed;
	public Vector3 rotSpeed;
	public float lessSpeed = 0.0f;
	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		transform.localPosition += moveSpeed * Time.deltaTime;
		transform.localEulerAngles += rotSpeed * Time.deltaTime;

		moveSpeed -= moveSpeed * lessSpeed * Time.deltaTime;
		rotSpeed -= rotSpeed * lessSpeed * Time.deltaTime;
		//moveSpeed *= (1f-lessSpeed);
		//rotSpeed *= (1f-lessSpeed);
	}
}
