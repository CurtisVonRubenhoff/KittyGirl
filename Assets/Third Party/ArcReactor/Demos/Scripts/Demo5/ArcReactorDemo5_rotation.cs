using UnityEngine;
using System.Collections;

namespace ArcReactor.Demo
{
	public class ArcReactorDemo5_rotation : MonoBehaviour {

		public float rotationSpeed;
		
				
		// Update is called once per frame
		void Update () 
		{
			transform.Rotate ( Vector3.forward * ( rotationSpeed * Time.deltaTime * Input.GetAxis("Vertical")) );
			transform.Rotate ( Vector3.up * ( rotationSpeed * Time.deltaTime * Input.GetAxis("Horizontal")) );
		}
	}
}