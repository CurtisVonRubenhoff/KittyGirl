// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Image Effects/Camera Glitch" 
{
	Properties 
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_GlitchTex ("Glitch Texture", 2D) = "white" {}
		_OverlayTex ("Overlay Texture", 2D) = "white" {}
		_ShowOverlay ("Show Overlay", Range (0, 1)) = 1
		_Intensity ("Glitch Intensity", Range(0.5, 2)) = 1.0
	}

	SubShader 
	{
		Pass 
		{
			ZTest Always 
			Cull Off 
			ZWrite Off
			Fog { Mode off }

			CGPROGRAM
			
			#include "UnityCG.cginc"
			
			#pragma target 3.0
			#pragma exclude_renderers flash
			#pragma vertex ComputeVertex
			#pragma fragment ComputeFragment
		
			uniform sampler2D _MainTex;
			uniform sampler2D _GlitchTex;
			uniform half4 _GlitchTex_ST;
			uniform sampler2D _OverlayTex;
			uniform fixed _ShowOverlay;
			uniform float _Intensity;
		
			uniform float filterRadius;
			uniform float flipUp, flipDown;
			uniform float displace;
			uniform float scale;
		
			struct vertexOutput 
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};
			
			float Random (float3 seed)
			{
				return frac(sin(dot(seed.xyz, float3(12.9898, 78.233, 45.5432))) * 43758.5453);
			}
			
			float4 ColorBurn (float4 a, float4 b) 
			{ 
				float4 r = float4(.0, .0, .0, b.a);
				r.r = 1.0 - (1.0 - a.r) / b.r;
				r.g = 1.0 - (1.0 - a.g) / b.g;
				r.b = 1.0 - (1.0 - a.b) / b.b;
				return r;
			}
			
			float4 Divide (float4 a, float4 b)
			{ 
				float4 r = float4(.0, .0, .0, b.a);
				r.r = a.r / b.r;
				r.g = a.g / b.g;
				r.b = a.b / b.b;
				return r; 
			}
		
			vertexOutput ComputeVertex (appdata_img v)
			{
				vertexOutput o;
				
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = v.texcoord.xy;

				return o;
			}
		
			fixed4 ComputeFragment (vertexOutput o) : COLOR
			{
				fixed4 mainColor = tex2D(_MainTex, o.uv.xy);
				fixed4 glitchColor = tex2D(_GlitchTex, o.uv.xy * _GlitchTex_ST.xy + _GlitchTex_ST.zw);
				fixed4 overlayColor = tex2D(_OverlayTex, o.uv.xy);
				
				mainColor = lerp(mainColor, lerp(mainColor, mainColor + overlayColor, overlayColor.a), _ShowOverlay);
				
				float glitchTrigger = Random(float3(_Time.x, _Time.y, _Time.z));
				
				if ((o.uv.y < glitchTrigger + filterRadius / 10 && o.uv.y > glitchTrigger - filterRadius / 10))
				{
					if (o.uv.y < flipUp)
						o.uv.y = 1.0 - (o.uv.y + flipUp);
				
					if (o.uv.y > flipDown)
						o.uv.y = 1.0 - (o.uv.y - flipDown);
						
					o.uv.xy += displace * _Intensity;
				}
				
				fixed4 redcolor = tex2D(_MainTex,  o.uv.xy + 0.01 * filterRadius * _Intensity);	
				fixed4 greencolor = tex2D(_MainTex,  o.uv.xy + 0.01 * filterRadius * _Intensity);
				
				if (filterRadius > 0.5)
				{
					mainColor.r = redcolor.r * 1.2;
					mainColor.b = greencolor.b * 1.2;
					mainColor = ColorBurn(mainColor, glitchColor);
				}
				else if (filterRadius < -0.5)
				{
					mainColor.g = redcolor.b * 1.2;
					mainColor.r = greencolor.g * 1.2;
					mainColor = Divide(mainColor, glitchColor);
				}
			
				return mainColor;
			}

			ENDCG
		}
	}

	Fallback off
}
