using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArcReactor.Demo
{
	public class ArcReactorDemo_SceneSelector : MonoBehaviour {

		public void Start()
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		public void SelectScene(string scene)
		{
			SceneManager.LoadScene(scene);
		}

		public void QuitApp()
		{
			Application.Quit();
		}
	}
}