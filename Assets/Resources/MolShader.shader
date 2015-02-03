Shader "Custom/MolShader" 
{	
	CGINCLUDE

	#include "UnityCG.cginc"
	
	struct AtomData
	{
	     float4 pos;
		 float4 info;
	};

	uniform float scale;		

	uniform	StructuredBuffer<int> molStates;
	uniform	StructuredBuffer<int> molTypes;
	uniform StructuredBuffer<int> molAtomCountBuffer;										
	uniform StructuredBuffer<int> molAtomStartBuffer;	
											
	uniform	StructuredBuffer<float4> molPositions;
	uniform	StructuredBuffer<float4> molRotations;	
	uniform StructuredBuffer<float4> atomDataPDBBuffer;			
	
	ENDCG
	
	SubShader 
	{	
		// First pass
	    Pass 
	    {
	    	CGPROGRAM			
	    		
			#include "UnityCG.cginc"
			
			#pragma only_renderers d3d11
			#pragma target 5.0				
			
			#pragma vertex VS				
			#pragma fragment FS
			#pragma hull HS
			#pragma domain DS	
			#pragma geometry GS			
		
			struct vs2hs
			{
	            float3 pos : CPOINT;
	            float4 rot : COLOR0;
	            float4 info : COLOR1;
        	};
        	
        	struct hsConst
			{
			    float tessFactor[2] : SV_TessFactor;
			};

			struct hs2ds
			{
			    float3 pos : CPOINT;
			    float4 rot : COLOR0;
			    float4 info : COLOR1;
			};
			
			struct ds2gs
			{
			    float3 pos : CPOINT;
			    float4 rot : COLOR0;
			    float4 info : COLOR1;
			    float4 info2 : COLOR2;
			};
			
			struct gs2fs
			{
			    float4 pos : SV_Position;
			    float4 worldPos : COLOR0;
			    float4 info : COLOR1;
			};
			
			float3 qtransform( float4 q, float3 v )
			{ 
				return v + 2.0 * cross(cross(v, q.xyz ) + q.w * v, q.xyz);
			}
				
			vs2hs VS(uint id : SV_VertexID)
			{
			    vs2hs output;						

			    output.pos = molPositions[id].xyz;	
			    output.rot = molRotations[id];

				float4 vPos = mul (UNITY_MATRIX_MV, float4(output.pos, 1.0));
				int lod = 1;

			    output.info = float4( molTypes[id], molStates[id], lod, id);
			    
			    return output;
			}										
			
			hsConst HSConst(InputPatch<vs2hs, 1> input, uint patchID : SV_PrimitiveID)
			{
				hsConst output;					
				
				float4 transformPos = mul (UNITY_MATRIX_MVP, float4(input[0].pos, 1.0));
				transformPos /= transformPos.w;
								
				float atomCount = floor(molAtomCountBuffer[input[0].info.x] / input[0].info.z) + 1;
									
				float tessFactor = min(ceil(sqrt(atomCount)), 64);
					
				//if(input[0].info.y < 0 || transformPos.x < -1 || transformPos.y < -1 || transformPos.x > 1 || transformPos.y > 1 || transformPos.z > 1 || transformPos.z < -1 ) 
				if(input[0].info.y < 0)				
				{
					output.tessFactor[0] = 0.0f;
					output.tessFactor[1] = 0.0f;
				}		
				else
				{
					output.tessFactor[0] = tessFactor;
					output.tessFactor[1] = tessFactor;					
				}		
				
				return output;
			}
			
			[domain("isoline")]
			[partitioning("integer")]
			[outputtopology("point")]
			[outputcontrolpoints(1)]				
			[patchconstantfunc("HSConst")]
			hs2ds HS (InputPatch<vs2hs, 1> input, uint ID : SV_OutputControlPointID)
			{
			    hs2ds output;
			    
			    output.pos = input[0].pos;
			    output.rot = input[0].rot;
			    output.info = input[0].info;
			    
			    return output;
			} 
			
			[domain("isoline")]
			ds2gs DS(hsConst input, const OutputPatch<hs2ds, 1> op, float2 uv : SV_DomainLocation)
			{
				ds2gs output;	

				int atomId = (uv.x * input.tessFactor[0] + uv.y * input.tessFactor[0] * input.tessFactor[1]);	
				
				output.pos = op[0].pos;
			    output.rot = op[0].rot;
			    output.info = op[0].info;	
			    
				output.info2.y = ceil((float) molAtomCountBuffer[op[0].info.x] / 4096.0);
				output.info2.x = atomId * output.info.z * output.info2.y;				
																
				return output;			
			}
			
			[maxvertexcount(8)]
			void GS(point ds2gs input[1], inout PointStream<gs2fs> pointStream)
			{
				for(int i = 0; i < input[0].info2.y; i++)
				{
					int atomId = input[0].info2.x + i;
					if(atomId < molAtomCountBuffer[input[0].info.x])
					{
						float4 atomDataPDB = atomDataPDBBuffer[atomId + molAtomStartBuffer[input[0].info.x]];	
				
						gs2fs output;
					
						output.worldPos = float4(input[0].pos + qtransform(input[0].rot, atomDataPDB.xyz) * scale, 1);
						output.pos = mul(UNITY_MATRIX_MVP, output.worldPos);
						output.info = float4(atomDataPDB.w, input[0].info.w, 0, 0);
					
						pointStream.Append(output);
					} 	
				}								  					
			}
			
			void FS (gs2fs input, out float4 pos : COLOR0, out float4 info : COLOR1)
			{								
				pos = input.worldPos;
				info = input.info;
			}
						
			ENDCG
		}
		
		// Second pass
		Pass
		{
			ZWrite Off ZTest Always Cull Off Fog { Mode Off }

			CGPROGRAM
			
			#include "UnityCG.cginc"
				
			#pragma only_renderers d3d11		
			#pragma target 5.0
			
			#pragma vertex vert_img
			#pragma fragment frag
		
			sampler2D posTex;
			sampler2D infoTex;
			
			AppendStructuredBuffer<AtomData> atomDataOutput : register(u1);
			
			void frag (v2f_img i, out float4 color : COLOR0)
			{
				AtomData output;

				output.pos = tex2D (posTex, i.uv);
				output.info = tex2D (infoTex, i.uv);
				
				if (output.pos.w > 0) atomDataOutput.Append (output);
			}			
			
			ENDCG
		}				
	}
	Fallback Off
}	