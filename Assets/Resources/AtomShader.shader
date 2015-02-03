Shader "Custom/MolShader" 
{
	CGINCLUDE

	#include "UnityCG.cginc"
	
	struct AtomData
	{
	     float4 pos;
		 float4 info;
	};

	uniform int showAtomColors;
	uniform float scale;	

	uniform	StructuredBuffer<int> molStates;
	uniform	StructuredBuffer<int> molTypes;
	uniform	StructuredBuffer<float> atomRadii;
	uniform StructuredBuffer<float4> molColors;		
	uniform	StructuredBuffer<float4> atomColors;	
	uniform StructuredBuffer<AtomData> atomDataBuffer;
	
	struct vs2gs
	{
		float4 pos : SV_POSITION;
		float4 info: COLOR0;
					
	};
			
	struct gs2fs
	{
		float4 pos : SV_Position;									
		float4 info: COLOR0;	
		float2 uv: TEXCOORD0;						
	};

	vs2gs VS(uint id : SV_VertexID)
	{
		AtomData atomData = atomDataBuffer[id];				   
			    
		vs2gs output;				    		    
		output.pos = atomData.pos;
		output.info = atomData.info;        
		return output;
	}
			
	[maxvertexcount(4)]
	void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
	{
		int atomId = round(input[0].info.x);
		int molId = round(input[0].info.y);

		if( atomId <  0) return;
				
		float radius = scale * atomRadii[atomId];
		float4 pos = mul(UNITY_MATRIX_MVP, float4(input[0].pos.xyz, 1.0));
		float4 offset = mul(UNITY_MATRIX_P, float4(radius, radius, 0, 1));

		gs2fs output;					
		output.info = float4(radius, atomId, molId, 0); 	   

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
			
	void FS (gs2fs input, out float4 color : COLOR0, out float4 normal_depth : COLOR1, out float depth : DEPTH) 
	{	
		float lensqr = dot(input.uv, input.uv);
    			
    	if(lensqr > 1.0) discard;
					
		float3 normal = normalize(float3(input.uv, sqrt(1.0 - lensqr)));		
				
		// Find depth
		float eyeDepth = LinearEyeDepth(input.pos.z) + input.info.x * -normal.z ;
		depth = 1 / (eyeDepth * _ZBufferParams.z) - _ZBufferParams.w / _ZBufferParams.z;
		normal_depth = EncodeDepthNormal (depth, normal);
		
		// Find color								
		float ndotl = max( 0.0, dot(float3(0,0,1), normal));										
		float3 atomColor = (showAtomColors > 0 ) ? atomColors[round(input.info.y)].rgb : molColors[round(input.info.z)].rgb;
		float3 finalColor = atomColor * pow(ndotl, 0.075);				
		color = float4(finalColor, 1);					
	}		

	ENDCG
	
	SubShader 
	{			
		Pass
		{		
			ZWrite On
						
			CGPROGRAM	
					
			#include "UnityCG.cginc"			
			
			#pragma vertex VS			
			#pragma fragment FS							
			#pragma geometry GS	
				
			#pragma only_renderers d3d11		
			#pragma target 5.0											
				
			ENDCG	
		}						
	}
	Fallback Off
}	