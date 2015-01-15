Shader "Custom/AtomShader" 
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	
	CGINCLUDE

	#include "UnityCG.cginc"
															
	uniform float scale;	

	uniform	StructuredBuffer<int> atomStates;
	uniform	StructuredBuffer<int> atomTypes;
	uniform	StructuredBuffer<float> atomAlphas;
	uniform	StructuredBuffer<float> atomRadii;
	uniform	StructuredBuffer<float4> atomColors;
	uniform	StructuredBuffer<float4> atomPositions;	
	
	ENDCG
	
	SubShader 	
	{	
		Tags {"Queue" = "Transparent" }

		// First pass
	    Pass 
	    {
			//Blend SrcAlpha OneMinusSrcAlpha
			//BlendOp Sub
			ZWrite On
			//Blend One One

	    	CGPROGRAM			
	    		
			#include "UnityCG.cginc"
			
			#pragma only_renderers d3d11
			#pragma target 5.0				
			
			#pragma vertex VS				
			#pragma fragment FS
			#pragma geometry GS			
		
			struct vs2gs
			{
	            float4 pos : SV_POSITION;
	            float4 info : COLOR0;
        	};        	
			
			struct gs2fs
			{
			    float4 pos : SV_POSITION;
				float4 info : COLOR0;
				float2 uv : TEXCOORD0;			    
			};
				
			vs2gs VS(uint id : SV_VertexID)
			{
				vs2gs output;
				output.pos = atomPositions[id]; 		
				output.info = float4(atomTypes[id], 0, 0, 0); 	    
			    return output;
			}										
						
			[maxvertexcount(4)]
			void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
			{
				if( round(input[0].info.x) <  0) return;
				
                float radius = scale * atomRadii[input[0].info.x] *1 ;
				float4 pos = mul(UNITY_MATRIX_MVP, float4(input[0].pos.xyz * scale, 1.0));
                float4 offset = mul(UNITY_MATRIX_P, float4(radius, radius, 0, 1));

				gs2fs output;					
				output.info = float4(radius, input[0].info.x, 0, 0); 	   

				//*****//

				output.uv = float2(1.0f, 1.0f);
				output.pos = pos + float4(output.uv * offset.xy, 0, 0);
				triangleStream.Append(output);

				output.uv = float2(1.0f, -1.0f);
				output.pos = pos + float4(output.uv * offset.xy, 0, 0);
				triangleStream.Append(output);	
								
				output.uv = float2(-1.0f, 1.0f);
				output.pos = pos + float4(output.uv * offset.xy, 0, 0);
				triangleStream.Append(output);

				output.uv = float2(-1.0f, -1.0f);
				output.pos = pos + float4(output.uv * offset.xy, 0, 0);
				triangleStream.Append(output);	
			}
			
			void FS (gs2fs input, out float4 color : SV_Target0 , out float4 normalDepth : SV_Target1, out float depth : SV_Depth) 
			{	
				float lensqr = dot(input.uv, input.uv);
    			
    			if(lensqr > 1.0)
        			discard;			    
							
				float3 normal = normalize(float3(input.uv, sqrt(1.0 - lensqr)));								              									
				float ndotl = min( 1, max( 0.0, dot(float3(0, 0, 1), normal)));	 	 			
				
				color = float4(atomColors[round(input.info.y)].rgb, 1) ;	
				//color = float4(1,1,1,1);	

				float eyeDepth = LinearEyeDepth(input.pos.z) + input.info.x * -normal.z ;													
				depth = 1 / (eyeDepth * _ZBufferParams.z) - _ZBufferParams.w / _ZBufferParams.z;	
				
				normalDepth = EncodeDepthNormal (depth, normal);   			
			}
						
			ENDCG
		}		
	}
	Fallback Off
}	