using UnityEngine;
using System.Collections;

namespace ArcReactor.Demo
{
	public class ArcReactorDemoTeleportZone : MonoBehaviour {

		public Vector3 teleportTo;

		void OnTriggerEnter(Collider other) {
			other.transform.position = teleportTo;
		}

	}
}