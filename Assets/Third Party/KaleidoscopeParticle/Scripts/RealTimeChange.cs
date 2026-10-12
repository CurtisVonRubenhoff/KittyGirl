using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RealTimeChange : MonoBehaviour {

	private int idx = 0;

	public Kaleidoscope kaleidoscope;
	public Transform kaleidoscopeTarget;
	public Mode renderMode;
	public Material[] materials;
	public Mesh[] meshes;
	public Slider slider;

	public enum Mode{
		MATERIAL,
		MESH,
	}

	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		Vector3 m_screen = Input.mousePosition;
		m_screen.z = 10f;
		Vector3 m_pos = Camera.main.ScreenToWorldPoint (m_screen);
		kaleidoscopeTarget.position = new Vector3 (
			m_pos.x,
			m_pos.y,
			kaleidoscopeTarget.position.z);
	}

	public void SetCircleValue(float value){
		kaleidoscope.circleValue = Mathf.RoundToInt(value);
	}

	public void OnSliderChanged(){
		SetCircleValue (slider.value);
	}

	public void OnButtonClick(){
		switch (renderMode) {
		case Mode.MATERIAL:
			idx = (idx + 1) % materials.Length;
			kaleidoscope.SetMaterial (materials [idx]);
			break;
		case Mode.MESH:
			idx = (idx + 1) % meshes.Length;
			kaleidoscope.SetMesh (meshes [idx]);
			break;
		default:
			break;
		}
	}
}
