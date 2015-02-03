Shader "Custom/DepthNormalBlitShader" 
{
	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		
		Pass 
		{
			ZWrite On
			ZTest Always

            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

			sampler2D _CameraDepthTexture;
			sampler2D _CameraDepthNormalsTexture;

            void frag(v2f_img i, out float4 color : COLOR0, out float depth : DEPTH) 
			{                
                color = tex2D(_CameraDepthNormalsTexture, i.uv);
				depth = tex2D(_CameraDepthTexture, i.uv).r;
            }
            ENDCG
        }

		//Pass 
		//{
		//	ZWrite On
		//	ZTest Always

  //          CGPROGRAM
  //          #pragma vertex vert_img
  //          #pragma fragment frag

  //          #include "UnityCG.cginc"

		//	sampler2D _CameraDepthTexture;

  //          void frag(v2f_img i, out float4 color : SV_Target0) 
		//	{                
		//		float cameraDepth = tex2D(_CameraDepthTexture, i.uv).r;
  //              color = float4(0, 0, Linear01Depth (cameraDepth), 1);
  //          }
  //          ENDCG
  //      }
	}	

	FallBack "Diffuse"
}