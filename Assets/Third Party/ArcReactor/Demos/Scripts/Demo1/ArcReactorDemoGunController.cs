using UnityEngine;
using System.Collections;

namespace ArcReactor.Demo
{
	public class ArcReactorDemoGunController : MonoBehaviour {

		public GunDefinition[] guns;
		public int selectedGun;
		[Tooltip("Visual width multiplier applied to the laser beam.")]
		public float beamThicknessMultiplier = 2.5f;

		[System.Serializable]
		public class GunDefinition
		{
			public ArcReactor_Launcher launcher;
			public float rechargeTime;
			public float currentRechargeTimeLeft;
			public bool continuous;
			public KeyCode keycode;
		}

		void Start ()
		{
			//Cursor.lockState = CursorLockMode.Locked;
			//Cursor.visible = false;
		}

		// Update is called once per frame
		void Update () 
		{
			if (!HasSelectedGun())
				return;

			if (guns[selectedGun].continuous)
			{
				if (Input.GetMouseButtonDown(0) && guns[selectedGun].launcher.Rays.Count == 0)
				{
					
				}
				if (Input.GetMouseButtonUp(0))
				{
					
				}
			}
			else
			{
				if (Input.GetMouseButton(0) && guns[selectedGun].currentRechargeTimeLeft == 0)
				{
					guns[selectedGun].launcher.LaunchRay();
					guns[selectedGun].currentRechargeTimeLeft = guns[selectedGun].rechargeTime;
				}
			}
		
		}


        public bool MouseDownShoot()
        {
            if (!HasSelectedGun() || guns[selectedGun].launcher.Rays.Count > 0)
                return false;
            ArcReactor_Launcher launcher = guns[selectedGun].launcher;
            launcher.LaunchRay();
            ApplyBeamThickness(launcher);
            return true;
        }

        private void ApplyBeamThickness(ArcReactor_Launcher launcher)
        {
            if (launcher.Rays.Count == 0)
                return;

            ArcReactor_Launcher.RayInfo ray = launcher.Rays[launcher.Rays.Count - 1];
            if (ray == null || ray.arc == null)
                return;

            ArcReactor_Arc sourceArc = launcher.arcPrefab != null
                ? launcher.arcPrefab.GetComponent<ArcReactor_Arc>()
                : null;
            float sourceSize = sourceArc != null ? sourceArc.sizeMultiplier : 1f;
            ray.arc.sizeMultiplier = sourceSize * Mathf.Max(1f, beamThicknessMultiplier);
        }

        public void MouseUpStop()
        {
            if (!HasSelectedGun())
                return;
            foreach (ArcReactor_Launcher.RayInfo ray in guns[selectedGun].launcher.Rays)
            {
                if (ray != null && ray.arc != null)
                {
                    ray.arc.playBackward = true;
                    ray.arc.freeze = false;
                }
            }

        }

        public void SetMinimumBeamDistance(float distance)
        {
            if (!HasSelectedGun())
                return;

            ArcReactor_Launcher launcher = guns[selectedGun].launcher;
            launcher.Distance = Mathf.Max(launcher.Distance, distance);
            launcher.PropagationSpeed = Mathf.Max(
                launcher.PropagationSpeed,
                launcher.Distance * 10f);

            // Gameplay damage is handled by PlayerController. Let the visual beam
            // pass through arena triggers such as EnemyStopper instead of ending
            // before it reaches the enemy formation.
            launcher.layers = 0;
            launcher.SendMessageToHitObjects = false;
        }

        public bool TryGetBeamVolume(out Transform origin, out float distance)
        {
            if (!HasSelectedGun())
            {
                origin = null;
                distance = 0f;
                return false;
            }

            origin = guns[selectedGun].launcher.transform;
            distance = guns[selectedGun].launcher.Distance;
            return true;
        }

        private bool HasSelectedGun()
        {
            return guns != null && selectedGun >= 0 && selectedGun < guns.Length &&
                guns[selectedGun] != null && guns[selectedGun].launcher != null;
        }




	}
}
