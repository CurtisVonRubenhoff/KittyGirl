using UnityEngine;
using UnityEditor;
using System.Collections;

[CustomEditor(typeof(ArcReactor_Arc))]
public class ArcReactor_Editor : Editor 
{
	public override void OnInspectorGUI() {
		ArcReactor_Arc arc = (ArcReactor_Arc)target;

		if (!(PrefabUtility.GetPrefabType(arc) == PrefabType.Prefab) && Mathf.Max(arc.shapeTransforms != null ? arc.shapeTransforms.Length : 0, arc.shapePoints != null ? arc.shapePoints.Length : 0) < 2)
		{
			EditorGUILayout.HelpBox("There should be at least 2 shape transforms or shape points for correct shape calculation.",MessageType.Warning);
		}
			
		if (arc.lifetime == 0)
			EditorGUILayout.HelpBox("Lifetime set to zero. This arc system will not be visible.",MessageType.Warning);
		
		if (arc.oscillationNormal == Vector3.zero)
			EditorGUILayout.HelpBox("Oscillation normal set to zero. Oscillation planes will be unpredictable.",MessageType.Error);
		
		if (arc.easeInOutOptions.useEaseInOut && arc.easeInOutOptions.distance == 0)
			EditorGUILayout.HelpBox("EaseInOut enabled but its distance set to zero. It will have no effect except performance hit.",MessageType.Info);


		if (arc.arcs == null || arc.arcs.Length == 0)
		{			
			//EditorGUILayout.HelpBox("Arcs array is empty. This arc system will not be visible.",MessageType.Warning);
			arc.arcs = new ArcReactor_Arc.LineRendererInfo[1];
			arc.arcs[0] = new ArcReactor_Arc.LineRendererInfo();

			arc.arcs[0].colorOptions = new ArcReactor_Arc.ArcColorOptions();
			arc.arcs[0].colorOptions.startColor = new Gradient();
			arc.arcs[0].colorOptions.startColor.SetKeys(new GradientColorKey[2]{new GradientColorKey(Color.red,0),new GradientColorKey(Color.red,1)},new GradientAlphaKey[2]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
			arc.arcs[0].colorOptions.endColor = new Gradient();
			arc.arcs[0].colorOptions.coreColor = new Gradient();
			arc.arcs[0].colorOptions.coreColor.SetKeys(new GradientColorKey[2]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new GradientAlphaKey[2]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
			arc.arcs[0].colorOptions.coreCurve = new AnimationCurve();
			arc.arcs[0].colorOptions.coreCurve.AddKey(0, 0.5f);
			arc.arcs[0].colorOptions.coreCurve.AddKey(1, 0.5f);

			arc.arcs[0].flaresOptions = new ArcReactor_Arc.ArcFlaresInfo();
			arc.arcs[0].flaresOptions.startFlare = new ArcReactor_Arc.FlareInfo();
			arc.arcs[0].flaresOptions.endFlare = new ArcReactor_Arc.FlareInfo();

			arc.arcs[0].sizeOptions = new ArcReactor_Arc.ArcSizeOptions();
			arc.arcs[0].sizeOptions.startWidthCurve = new AnimationCurve();
			arc.arcs[0].sizeOptions.startWidthCurve.AddKey(0, 0.5f);
			arc.arcs[0].sizeOptions.startWidthCurve.AddKey(1, 0.5f);
			arc.arcs[0].sizeOptions.endWidthCurve = new AnimationCurve();
			arc.arcs[0].sizeOptions.endWidthCurve.AddKey(0, 0.5f);
			arc.arcs[0].sizeOptions.endWidthCurve.AddKey(1, 0.5f);

			arc.arcs[0].lightsOptions = new ArcReactor_Arc.ArcLightsOptions();

			arc.arcs[0].nesting = new ArcReactor_Arc.ArcNestingOptions();

			arc.arcs[0].propagationOptions = new ArcReactor_Arc.ArcPropagationOptions();

			arc.arcs[0].textureOptions = new ArcReactor_Arc.TextureAnimationOptions();

			arc.arcs[0].oscillations = new ArcReactor_Arc.OscillationInfo[0];
		}

		for (int i = 0; i < arc.arcs.Length; i++)
		{
			if ((arc.arcs[i].flaresOptions.startFlare.enabled || arc.arcs[i].flaresOptions.endFlare.enabled) && arc.currentCamera == null)
				arc.currentCamera = Camera.main;

			if (arc.arcs[i].sizeOptions.segmentLength <= 0)
			{
				//EditorGUILayout.HelpBox("Segment Length of Arc #"+i.ToString()+" is set to zero or lower. It would cause unexpected behaviour or division by zero errors.",MessageType.Error);
				arc.arcs[i].sizeOptions.segmentLength = 1;
			}

			if (arc.arcs[i].colorOptions.startColor.colorKeys.Length == 2
				&& arc.arcs[i].colorOptions.startColor.colorKeys[0].color == Color.black
				&& arc.arcs[i].colorOptions.startColor.colorKeys[0].time == 0
				&& arc.arcs[i].colorOptions.startColor.colorKeys[1].color == Color.black
				&& arc.arcs[i].colorOptions.startColor.colorKeys[1].time == 1
				&& arc.arcs[i].colorOptions.startColor.alphaKeys.Length == 2
				&& arc.arcs[i].colorOptions.startColor.alphaKeys[0].alpha == 0
				&& arc.arcs[i].colorOptions.startColor.alphaKeys[0].time == 0
				&& arc.arcs[i].colorOptions.startColor.alphaKeys[1].alpha == 0
				&& arc.arcs[i].colorOptions.startColor.alphaKeys[1].time == 1)
			{
				arc.arcs[i].colorOptions.startColor.SetKeys(new GradientColorKey[2]{new GradientColorKey(Color.red,0),new GradientColorKey(Color.red,1)},new GradientAlphaKey[2]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
			}

			if (arc.arcs[i].colorOptions.coreColor.colorKeys.Length == 2
				&& arc.arcs[i].colorOptions.coreColor.colorKeys[0].color == Color.black
				&& arc.arcs[i].colorOptions.coreColor.colorKeys[0].time == 0
				&& arc.arcs[i].colorOptions.coreColor.colorKeys[1].color == Color.black
				&& arc.arcs[i].colorOptions.coreColor.colorKeys[1].time == 1
				&& arc.arcs[i].colorOptions.coreColor.alphaKeys.Length == 2
				&& arc.arcs[i].colorOptions.coreColor.alphaKeys[0].alpha == 0
				&& arc.arcs[i].colorOptions.coreColor.alphaKeys[0].time == 0
				&& arc.arcs[i].colorOptions.coreColor.alphaKeys[1].alpha == 0
				&& arc.arcs[i].colorOptions.coreColor.alphaKeys[1].time == 1)
			{
				arc.arcs[i].colorOptions.coreColor.SetKeys(new GradientColorKey[2]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new GradientAlphaKey[2]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
			}

			if (arc.arcs[i].colorOptions.coreCurve.keys.Length == 0)
			{					
				arc.arcs[i].colorOptions.coreCurve.AddKey(0, 0.5f);
				arc.arcs[i].colorOptions.coreCurve.AddKey(1, 0.5f);
			}
				
			if (arc.arcs[i].sizeOptions.startWidthCurve.keys.Length == 0)
			{					
				arc.arcs[i].sizeOptions.startWidthCurve.AddKey(0, 0.5f);
				arc.arcs[i].sizeOptions.startWidthCurve.AddKey(1, 0.5f);
			}

			if (arc.arcs[i].sizeOptions.shapeType == ArcReactor_Arc.ShapeTypes.start_end && arc.arcs[i].sizeOptions.endWidthCurve.keys.Length == 0)
			{					
				arc.arcs[i].sizeOptions.endWidthCurve.AddKey(0, 0.5f);
				arc.arcs[i].sizeOptions.endWidthCurve.AddKey(1, 0.5f);
			}

			if (arc.arcs[i].material == null)
			{
				//Debug.LogWarning(gameObject.name + " : Material has not been assigned to Arc #" + i.ToString() + ", setting default material.");
				arc.arcs[i].material = Resources.Load("DefaultArcReactorMat_ThinArc",typeof(Material)) as Material;
			}

			if (arc.arcs[i].nesting.Nested && arc.arcs[i].nesting.parentArcIndex > i)
				EditorGUILayout.HelpBox("Arc #" + i.ToString() + " is nested to arc with higher index. That's not recommended because of vertex caching.", MessageType.Info);

			for (int q = 0; q < arc.arcs[i].oscillations.Length; q++)
			{
				if (arc.arcs[i].oscillations[q].amplitude == 0)
				{
					EditorGUILayout.HelpBox("Amplitude of oscillation #" + q.ToString() + " of Arc #" + i.ToString() + " set to zero. It will have no effect except performance hit",MessageType.Info);
				}
				if (arc.arcs[i].oscillations[q].wavelength == 0)
				{
					EditorGUILayout.HelpBox("Wavelength of oscillation #" + q.ToString() + " of Arc #" + i.ToString() + " set to zero. That makes no mathematical sense.",MessageType.Error);
				}
			}
		}		

		// Show default inspector property editor
		DrawDefaultInspector ();
	}

}
