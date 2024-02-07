using UnityEngine;

[RequireComponent(typeof(Camera))]
[AddComponentMenu("Image Effects/Camera Glitch"), ExecuteInEditMode]
public class CameraGlitch : MonoBehaviour
{
	public Texture GlitchTexture;
	public Texture OverlayTexture;
	public float Intensity = 1.0f;
	public bool ShowOverlay;

	private float glitchUp, glitchDown, flicker, glitchUpTime = .05f, glitchDownTime = .05f, flickerTime = .5f;
	private static Shader shader;
	private Material _material;
	private Material material
	{
		get
		{
			if (_material == null)
			{
				_material = new Material(shader);
				_material.SetTexture("_GlitchTex", GlitchTexture ? GlitchTexture : Resources.Load("GlitchTexture") as Texture);
				_material.SetTextureScale("_GlitchTex", new Vector2(Screen.width / (GlitchTexture ? (float)GlitchTexture.width : 512f), 
					Screen.height / (GlitchTexture ? (float)GlitchTexture.height : 512f)));
				_material.SetTexture("_OverlayTex", OverlayTexture ? OverlayTexture : Resources.Load("OverlayTexture") as Texture);
				_material.hideFlags = HideFlags.HideAndDontSave;
			}
			return _material;
		}
	}

	private void Start ()
	{
		if (!shader) shader = Resources.Load("CameraGlitch") as Shader;

		if (!SystemInfo.supportsImageEffects || !shader || !shader.isSupported)
			enabled = false;
	}

	private void OnDisable ()
	{
		if (_material) DestroyImmediate(_material);
	}

	private void OnRenderImage (RenderTexture source, RenderTexture destination)
	{
		material.SetFloat("_Intensity", Intensity);
		material.SetFloat("_ShowOverlay", ShowOverlay ? 1 : 0);

		if (Intensity == 0) material.SetFloat("filterRadius", 0);

		glitchUp += Time.deltaTime * Intensity;
		glitchDown += Time.deltaTime * Intensity;
		flicker += Time.deltaTime * Intensity;

		if (flicker > flickerTime)
		{
			material.SetFloat("filterRadius", Random.Range(-3f, 3f) * Intensity);
			material.SetTextureOffset("_GlitchTex", new Vector2(Random.Range(-3f, 3f), Random.Range(-3f, 3f)));
			flicker = 0;
			flickerTime = Random.value;
		}

		if (glitchUp > glitchUpTime)
		{
			if (Random.value < .1f * Intensity) material.SetFloat("flipUp", Random.Range(0f, 1f) * Intensity);
			else material.SetFloat("flipUp", 0);

			glitchUp = 0;
			glitchUpTime = Random.value / 10f;
		}

		if (glitchDown > glitchDownTime)
		{
			if (Random.value < .1f * Intensity) material.SetFloat("flipDown", 1f - Random.Range(0f, 1f) * Intensity);
			else material.SetFloat("flipDown", 1f);

			glitchDown = 0;
			glitchDownTime = Random.value / 10f;
		}

		if (Random.value < .05f * Intensity)
		{
			material.SetFloat("displace", Random.value * Intensity);
			material.SetFloat("scale", 1f - Random.value * Intensity);
		}
		else material.SetFloat("displace", 0);

		Graphics.Blit(source, destination, material);
	}
}