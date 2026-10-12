using UnityEngine;
using System.Collections;

public class AutoWaveMove : MonoBehaviour {
	public Vector3 moveSpeed;
	public float waveTime = 1f;
	float time = 0;
	Vector3 default_pos;
	// Use this for initialization
	void Start () {
		default_pos = transform.localPosition;
	}
	
	// Update is called once per frame
	void Update () {
		time += Time.deltaTime;
		transform.localPosition = default_pos + moveSpeed * Mathf.Sin (Mathf.PI * 2 * (time / waveTime));
	}
}
