Shader "Custom/TunnelShader" 
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	
	CGINCLUDE

	#include "UnityCG.cginc"
															
	uniform	StructuredBuffer<float4> tunnelPositions;
	uniform	StructuredBuffer<float4> tunnelColors;
	uniform	StructuredBuffer<float> tunnelRadii;	
	
	uniform float scale;	
	
	ENDCG
	
	SubShader 
	{	
		// First pass
	    Pass 
	    {
			Blend SrcAlpha OneMinusSrcAlpha

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
				float4 color : COLOR0;
	            float4 info : COLOR1;
        	};        	
			
			struct gs2fs
			{
			    float4 pos : SV_POSITION;
				float4 color : COLOR0;
				float4 info : COLOR1;
				float2 uv : TEXCOORD0;			    
			};
				
			vs2gs VS(uint id : SV_VertexID)
			{
				vs2gs output;			
				output.pos = tunnelPositions[id]; 
				output.color = tunnelColors[id]; 		
				output.info = float4(tunnelRadii[id], 0, 0, 0); 	    
			    return output;
			}										
						
			[maxvertexcount(4)]
			void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
			{
				if( round(input[0].info.x) <  0) return;
				
                float radius = scale * input[0].info.x * 0.5;
				float4 pos = mul(UNITY_MATRIX_MVP, float4(input[0].pos.xyz * scale, 1.0));
                float4 offset = mul(UNITY_MATRIX_P, float4(radius, radius, 0, 1));

				gs2fs output;					
				output.color = input[0].color;
				output.info = float4(radius, 0, 0, 0); 	   

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
			
			void FS (gs2fs input, out float4 fragColor : COLOR0, out float fragDepth : DEPTH) 
			{								
				float lensqr = dot(input.uv, input.uv);
    			
    			if(lensqr > 1.0)
        			discard;			    
				
				float3 light = float3(0, 0, 1);  				
				float3 normal = normalize(float3(input.uv, sqrt(1.0 - lensqr)));				              									
				float ndotl = min( 1, max( 0.0, dot(light, normal)));		
				
				fragColor = float4(input.color.rgb * ndotl, input.color.a) ;		
				
				float eyeDepth = LinearEyeDepth(input.pos.z) + input.info.x * -normal.z;													
				fragDepth = 1 / (eyeDepth * _ZBufferParams.z) - _ZBufferParams.w / _ZBufferParams.z;		
			}
						
			ENDCG
		}		

		// First pass
	    Pass 
	    {
			Blend SrcAlpha OneMinusSrcAlpha

			ZWrite On

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
				float4 color : COLOR0;
	            float4 info : COLOR1;
        	};        	
			
			struct gs2fs
			{
			    float4 pos : SV_POSITION;
				float4 color : COLOR0;
				float4 info : COLOR1;
				float2 uv : TEXCOORD0;			    
			};
				
			vs2gs VS(uint id : SV_VertexID)
			{
				vs2gs output;			
				output.pos = tunnelPositions[id]; 
				output.color = tunnelColors[id]; 		
				output.info = float4(tunnelRadii[id], 0, 0, 0); 	    
			    return output;
			}										
						
			[maxvertexcount(4)]
			void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
			{
				if( round(input[0].info.x) <  0) return;
				
                float radius = scale * input[0].info.x * 0.5;
				float4 pos = mul(UNITY_MATRIX_MVP, float4(input[0].pos.xyz * scale, 1.0));
                float4 offset = mul(UNITY_MATRIX_P, float4(radius, radius, 0, 1));

				gs2fs output;					
				output.color = input[0].color;
				output.info = float4(radius, 0, 0, 0); 	   

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
			
			void FS (gs2fs input, out float4 fragColor : COLOR0) 
			{								
				float lensqr = dot(input.uv, input.uv);
    			
    			if(lensqr > 1.0)
        			discard;			    
				
				float3 light = float3(0, 0, 1);  				
				float3 normal = normalize(float3(input.uv, sqrt(1.0 - lensqr)));				              									
				float ndotl = min( 1, max( 0.0, dot(light, normal)));		
				
				fragColor = float4(input.color.rgb, 0.75);	
															
					
			}
						
			ENDCG
		}		
	}
	Fallback Off
}	