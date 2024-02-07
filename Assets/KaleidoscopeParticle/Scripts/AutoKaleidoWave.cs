using UnityEngine;
using System.Collections;

public class AutoKaleidoWave : MonoBehaviour {
	
	public Vector3 ringShiftSpeed;
	public float ringShiftTime = 1f;
	public float ringAngleSpeed;
	public float ringAngleTime = 1f;

	Kaleidoscope kaleidoscope;
	float time = 0;
	Vector3 def_ringShift;
	float def_ringAngle;

	// Use this for initialization
	void Start () {
		kaleidoscope = GetComponent<Kaleidoscope> ();
		def_ringShift = kaleidoscope.ringShift;
		def_ringAngle = kaleidoscope.ringAngle;
	}
	
	// Update is called once per frame
	void Update () {
		time += Time.deltaTime;
		kaleidoscope.ringShift = def_ringShift + ringShiftSpeed * Mathf.Sin (Mathf.PI * 2 * (time / ringShiftTime));
		kaleidoscope.ringAngle = def_ringAngle + ringAngleSpeed * Mathf.Sin (Mathf.PI * 2 * (time / ringAngleTime));
	}
}
