using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;


[AddComponentMenu("Arc Reactor Rays/Ray Trail")]
public class ArcReactor_Trail : MonoBehaviour {

	[Tooltip("Prefab with ArcReactor_Arc component used for visualisation.")]
	public GameObject arcPrefab;
	[Tooltip("Sets limit to maximum trail length.")]
	public bool truncateByDistance;
	[Tooltip("Trail length will be no longer than this value if truncateByDistance is on.")]
	public float distanceThreshold = 10;
	[Tooltip("Sets limit to lifetime of trail points.")]
	public bool truncateByLifetime;
	[Tooltip("All points olders than this will be removed.")]
	public float lifetimeThreshold = 1;
	[Tooltip("Creates new point if difference between current position and previous point is higher than this value.")]
	public float precision = 0.01f;
	[Tooltip("All spatial calculations will be done in space of this object. Leave null to use Unity global space.")]
	public Transform globalSpaceTransform;

	public List<SegmentInfo> segments;
	[HideInInspector]
	public ArcReactor_Arc currentArc;

	//protected bool initFlag = false;
	
	public ArcReactor_Arc DetachRay (bool newshape = false)
	{
		ArcReactor_Arc tempArc = currentArc;
		currentArc = null;
		if (newshape)
			Initialize();
		return tempArc;
	}

	public class SegmentInfo
	{
		public Vector3 pos;
		public float birthtime;
		public SegmentInfo(Vector3 pos, float birthtime)
		{
			this.pos = pos;
			this.birthtime = birthtime;
		}
	}



	// Use this for initialization
	void Awake () 
	{
		segments = new List<SegmentInfo>();
	}


	void Initialize()
	{
		segments.Clear();
		//segments.Add(new SegmentInfo(transform.position,Time.time));
	}



	void Update()
	{
		if (currentArc == null && segments.Count > 1)
		{
			GameObject obj = (GameObject)Instantiate(arcPrefab);
			currentArc = obj.GetComponent<ArcReactor_Arc>();
			if (globalSpaceTransform != null)
				obj.transform.parent = globalSpaceTransform;
		}

		if (currentArc != null)
		{			
			bool needFinalPoint = (transform.position - segments[segments.Count-1].pos).sqrMagnitude > 0.001;

			if (!needFinalPoint && segments.Count < 2)
			{
				if (ArcReactor_PoolManager.Instance != null)
					ArcReactor_PoolManager.Instance.SetEntityAsFree(currentArc);
				else
					Destroy(currentArc);
			}

			int shapePointsSize = segments.Count;
			if (needFinalPoint)
				shapePointsSize++;

			if (currentArc.shapePoints.Length != shapePointsSize)
			{
				Array.Resize(ref currentArc.shapePoints,shapePointsSize);
			}

			if (needFinalPoint)
			{
				currentArc.shapePoints[0] = transform.position;
				for (int x = 0; x < segments.Count; x++)
				{
					currentArc.shapePoints[segments.Count - x] = segments[x].pos;
				}
			}
			else
			{
				for (int x = 0; x < segments.Count; x++)
				{
					currentArc.shapePoints[segments.Count - x - 1] = segments[x].pos;
				}
			}
		}

	}

	void LateUpdate () 
	{
		if (truncateByLifetime && segments.Count > 1)
		{
			if (Time.time - segments[segments.Count-1].birthtime > lifetimeThreshold)
			{
				Initialize();
			}
			else
			{
				for (int i = 0; i < segments.Count-1; i++)
				{
					if (Time.time - segments[segments.Count-1-i].birthtime > lifetimeThreshold)
					{
						segments.RemoveRange(0,segments.Count-2-i);
						break;
					}
				}
			}
		}


		if (truncateByDistance && segments.Count > 1)
		{
			float distance = Vector3.Distance(transform.position,segments[segments.Count-1].pos);
			if (distance > distanceThreshold)
			{
				Initialize();
			}
			else
			{
				for (int i = 0; i < segments.Count-1; i++)
				{
					distance += Vector3.Distance(segments[segments.Count-1-i].pos,segments[segments.Count-2-i].pos);
					if (distance > distanceThreshold)
					{
						segments.RemoveRange(0,segments.Count-2-i);
						break;
					}
				}
			}
		}

		if (segments.Count == 0 || Vector3.SqrMagnitude(transform.position - segments[segments.Count - 1].pos) > precision * precision)
		{
			segments.Add(new SegmentInfo(transform.position,Time.time));
		}			
	}
}
