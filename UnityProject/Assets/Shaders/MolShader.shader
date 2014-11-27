Shader "Custom/MolShader" 
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}
	
	CGINCLUDE

	#include "UnityCG.cginc"
	
	struct AtomData
	{
	     float x;
	     float y;
	     float z;
	     float r;
	     float t;
	     float s;
	     float id;
	};

	uniform StructuredBuffer<AtomData> atomDataBuffer;
	uniform StructuredBuffer<float4> atomDataPDBBuffer;		
	uniform StructuredBuffer<int> molAtomCountBuffer;										
	uniform StructuredBuffer<int> molAtomStartBuffer;											
	uniform	StructuredBuffer<float4> molPositions;
	uniform	StructuredBuffer<float4> molRotations;	
	uniform StructuredBuffer<float4> molColors;		
	uniform	StructuredBuffer<int> molStates;
	uniform	StructuredBuffer<int> molTypes;
	
	uniform float molScale;	
	uniform float cameraPosition;	
//	uniform float4 viewDirection;	

	float Epsilon = 1e-10;	
	
	float3 HSVtoRGB(float3 HSV)
	{
	    float3 RGB = 0;
	    float C = HSV.z * HSV.y;
	    float H = HSV.x * 6;
	    float X = C * (1 - abs(fmod(H, 2) - 1));
	    if (HSV.y != 0)
	    {
	        float I = floor(H);
	        if (I == 0) { RGB = float3(C, X, 0); }
	        else if (I == 1) { RGB = float3(X, C, 0); }
	        else if (I == 2) { RGB = float3(0, C, X); }
	        else if (I == 3) { RGB = float3(0, X, C); }
	        else if (I == 4) { RGB = float3(X, 0, C); }
	        else { RGB = float3(C, 0, X); }
	    }
	    float M = HSV.z - C;
	    return RGB + M;
	}

	float3 RGBtoHSV(float3 RGB)
	{
	    float3 HSV = 0;
	    float M = min(RGB.r, min(RGB.g, RGB.b));
	    HSV.z = max(RGB.r, max(RGB.g, RGB.b));
	    float C = HSV.z - M;
	    if (C != 0)
	    {
	        HSV.y = C / HSV.z;
	        float3 D = (((HSV.z - RGB) / 6) + (C / 2)) / C;
	        if (RGB.r == HSV.z)
	            HSV.x = D.b - D.g;
	        else if (RGB.g == HSV.z)
	            HSV.x = (1.0/3.0) + D.r - D.b;
	        else if (RGB.b == HSV.z)
	            HSV.x = (2.0/3.0) + D.g - D.r;
	        if ( HSV.x < 0.0 ) { HSV.x += 1.0; }
	        if ( HSV.x > 1.0 ) { HSV.x -= 1.0; }
	    }
	    return HSV;
	}

	float3 ModifyColor(float3 color, float s, float v)
	{
		float3 c = RGBtoHSV(color);		
		c.y = s;
		c.z = v;
		return 	HSVtoRGB(c);	
	}
	
	float4 GetColor(int type, int state)
	{
		if(state == 0)
			return float4( ModifyColor(molColors[type], 0.6, 0.75), 1);	// return float4( 0, 0, 0, 1);			
		else if (state == 1)
			return float4( ModifyColor(molColors[type], 0.5, 0.01), 1);	// return float4( 0, 0, 0, 1);	
		
		return float4( 1, 1, 1, 1);	
	}
	
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
			    
				int lod = 1;	

			    output.pos = molPositions[id].xyz;	
			    output.rot = molRotations[id];
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
					
				if(input[0].info.y == -1 || transformPos.x < -1 || transformPos.y < -1 || transformPos.x > 1 || transformPos.y > 1 || transformPos.z > 1 || transformPos.z < -1 ) 
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
			    output.info2.x = atomId * output.info.z;
																
				return output;			
			}
			
			[maxvertexcount(1)]
			void GS(point ds2gs input[1], inout PointStream<gs2fs> pointStream)
			{
				if(input[0].info2.x < molAtomCountBuffer[input[0].info.x])
				{
					float4 atomDataPDB = atomDataPDBBuffer[input[0].info2.x + molAtomStartBuffer[input[0].info.x]];	
				
					gs2fs output;
					
					output.worldPos = float4(input[0].pos + qtransform(input[0].rot, atomDataPDB.xyz) * molScale, atomDataPDB.w);
					output.pos = mul(UNITY_MATRIX_MVP, float4(output.worldPos.xyz, 1));
					output.info = input[0].info.xyzw;
					
					pointStream.Append(output);
				} 					  					
			}
			
			void FS (gs2fs input, out float4 color1 : COLOR0, out float4 color2 : COLOR1)
			{								
				color1 = input.worldPos;
				color2 = input.info;
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
			
			#pragma vertex vert
			#pragma fragment frag
		
			sampler2D posTex;
			sampler2D infoTex;
			
			AppendStructuredBuffer<AtomData> pointBufferOutput : register(u1);

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};			

			v2f vert (appdata_base v)
			{
				v2f o;
				o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
				o.uv = v.texcoord;
				return o;
			}			

			float4 frag (v2f i) : COLOR0
			{
				float4 pos = tex2D (posTex, i.uv);
				float4 info = tex2D (infoTex, i.uv);
				
				AtomData o;
				o.x = pos.x;
				o.y = pos.y;
				o.z = pos.z;
				o.r = pos.w;				
				o.t = info.x;
				o.s = info.y;
				o.id = info.w;
				
				[branch]
				if (any(pos > 0))
				{
					pointBufferOutput.Append (o);
				}
				
				discard;
				return pos;
			}			
			
			ENDCG
		}			
		

		// Third pass
		Pass
		{	
			CGPROGRAM	
					
			#include "UnityCG.cginc"			
			
			#pragma vertex VS			
			#pragma fragment FS							
			#pragma geometry GS	
				
			#pragma only_renderers d3d11		
			#pragma target 5.0									
											
			struct vs2gs
			{
				float4 pos : SV_POSITION;
				float4 info: COLOR0;	
			};
			
			struct gs2fs
			{
				float4 pos : SV_POSITION;							
				float4 info: COLOR0;					
			};

			vs2gs VS(uint id : SV_VertexID)
			{
			    AtomData atomData = atomDataBuffer[id];	
			    
			    vs2gs output;	
			    output.pos = mul (UNITY_MATRIX_MV, float4(atomData.x, atomData.y, atomData.z, 1));
			    output.info = float4(atomData.r, atomData.t, atomData.s, atomData.id); 
			    					        
			    return output;
			}
			
			[maxvertexcount(1)]
			void GS(point vs2gs input[1], inout PointStream<gs2fs> pointStream)
			{
				gs2fs output;	
				  	
			  	output.pos = mul (UNITY_MATRIX_P, input[0].pos);							
				output.info = input[0].info;
					
				pointStream.Append(output);				
			}
			
			void FS (gs2fs input, out float4 fragColor : COLOR0, out float fragID : COLOR1) 
			{			
				fragColor = GetColor(round(input.info.y), round(input.info.z));	
				fragID = input.info.w;		
			}
			
			ENDCG					
		}	
		
		// Fourth pass
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
			    float4 atomPos = float4(atomData.x, atomData.y, atomData.z, 1);
			    
			    vs2gs output;				    		    
			    output.pos = mul(UNITY_MATRIX_MV, atomPos);
			    output.info = float4(atomData.r, atomData.t, atomData.s, atomData.id); 					        
			    return output;
			}
			
			[maxvertexcount(4)]
			void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
			{
				float spriteSize = (molScale * input[0].info.x);
								
				gs2fs output;	
			  	output.info = float4(spriteSize, input[0].info.y, input[0].info.z, input[0].info.w);
				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(spriteSize, spriteSize, 0, 0));
				output.uv = float2(1.0f, 1.0f);
				triangleStream.Append(output);

				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(spriteSize, -spriteSize, 0, 0));
				output.uv = float2(1.0f, -1.0f);
				triangleStream.Append(output);					

				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(-spriteSize, spriteSize, 0, 0));
				output.uv = float2(-1.0f, 1.0f);
				triangleStream.Append(output);

				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(-spriteSize, -spriteSize, 0, 0));
				output.uv = float2(-1.0f, -1.0f);
				triangleStream.Append(output);	
				
				triangleStream.RestartStrip();	
			}
			
			void FS (gs2fs input, in float4 screenSpace : SV_Position, out float4 fragColor : COLOR0, out float fragID : COLOR1, out float fragDepth : DEPTH) 
			{	
				float lensqr = dot(input.uv, input.uv);
    			
    			if(lensqr > 1.0)
        			discard;

			    float3 normal = normalize(float3(input.uv, sqrt(1.0 - lensqr)));				
				float atomEyeDepth = LinearEyeDepth(input.pos.z);		
				
				float3 light = float3(0, 0, 1);                									
				float ndotl = max( 0.0, dot(light, normal));			
//				float rimPower = 1.5;
//				float rim = 1.0 - saturate(dot (normalize(float4(0.25,0,1,0)), normalize(normal)));  									
//				fragColor = atomColor  + float4(0.25,0.25,0.25,0) * pow (rim, rimPower);
				
				float4 color = GetColor(round(input.info.y), round(input.info.z));	

				fragColor = color * 0.85 + color * ndotl * 0.15;			
				fragDepth = 1 / ((atomEyeDepth + input.info.x * -normal.z) * _ZBufferParams.z) - _ZBufferParams.w / _ZBufferParams.z;		
				fragID = input.info.w;	
			}			
			ENDCG	
		}
		
//		Pass
//		{
//            CGPROGRAM
//            #pragma vertex vert_img
//            #pragma fragment frag
//
//            #include "UnityCG.cginc"
//
//			sampler2D _MainTex;
//			sampler2D _DepthTex;
//			
//            float4 frag(v2f_img i) : COLOR 
//            {
//            	float d = LinearEyeDepth(tex2D (_DepthTex, i.uv).r);
//                return float4(d,d,d,1);
//            }
//            ENDCG
//        }
				
//		Pass
//		{
//            CGPROGRAM
//            #pragma vertex vert_img
//            #pragma fragment frag
//
//            #include "UnityCG.cginc"
//
//			sampler2D_float _DepthBufferTex;
//			
//            void frag(v2f_img i, out float fragDepth : COLOR )
//            {     
//            	fragDepth = tex2D (_DepthBufferTex, i.uv);  
//          	}
//          	ENDCG
//		}
		
		Pass
		{
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

			sampler2D_float _MainTex;
			sampler2D_float _DepthTex;
			sampler2D_float _IDTex;
			
            void frag(v2f_img i, out float4 fragNormal : COLOR )
            {               	
            	float4 inputNormal = tex2D (_MainTex, i.uv);
            	float inputDepth = tex2D (_DepthTex, i.uv);
            	float inputID = tex2D (_IDTex, i.uv);  
            	
            	if(inputDepth == 1 || inputID <= 0)
            	{            	
            		discard;
            	} 
            					
				float blurSize = 1.0 / _ScreenParams.x;
				float3 sum = float3(0.0, 0, 0); 				
 				
				// blur in x (horizontal)
				sum += tex2D(_MainTex, float2(i.uv.x - 4.0*blurSize, i.uv.y)) * 0.05;
				sum += tex2D(_MainTex, float2(i.uv.x - 3.0*blurSize, i.uv.y)) * 0.09;
				sum += tex2D(_MainTex, float2(i.uv.x - 2.0*blurSize, i.uv.y)) * 0.12;
				sum += tex2D(_MainTex, float2(i.uv.x - blurSize, i.uv.y)) * 0.15;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y)) * 0.16;
				sum += tex2D(_MainTex, float2(i.uv.x + blurSize, i.uv.y)) * 0.15;
				sum += tex2D(_MainTex, float2(i.uv.x + 2.0*blurSize, i.uv.y)) * 0.12;
				sum += tex2D(_MainTex, float2(i.uv.x + 3.0*blurSize, i.uv.y)) * 0.09;
				sum += tex2D(_MainTex, float2(i.uv.x + 4.0*blurSize, i.uv.y)) * 0.05;
				
				fragNormal = float4(sum, 1);		
            }
            ENDCG
        }
		
		// Vertical depth blur
		Pass
		{
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

			sampler2D_float _MainTex;
			sampler2D_float _DepthTex;
			sampler2D_float _IDTex;
			
            void frag(v2f_img i, out float4 fragNormal : COLOR )
            {       
            	float4 inputNormal = tex2D (_MainTex, i.uv);
            	float inputDepth = tex2D (_DepthTex, i.uv);
            	float inputID = tex2D (_IDTex, i.uv);  
            	
            	if(inputDepth == 1 || inputID <= 0)
            	{            	
            		discard;
            	}
				
				float blurSize = 1.0 / _ScreenParams.y;
				float3 sum = float3(0.0, 0, 0); 				
 				
				// blur in y (vertical)
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y - 4.0*blurSize)) * 0.05;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y - 3.0*blurSize)) * 0.09;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y - 2.0*blurSize)) * 0.12;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y - blurSize)) * 0.15;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y)) * 0.16;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y + blurSize)) * 0.15;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y + 2.0*blurSize)) * 0.12;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y + 3.0*blurSize)) * 0.09;
				sum += tex2D(_MainTex, float2(i.uv.x, i.uv.y + 4.0*blurSize)) * 0.05;
				
				fragNormal = float4(sum, 1);				
            }
            ENDCG
        }
        
		Pass
		{
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

			sampler2D_float _MainTex;
			sampler2D_float _DepthTex;
			sampler2D_float _IDTex;
			
            void frag(v2f_img i, out float4 fragColor : COLOR )
            {     
            	float4 inputNormal = tex2D (_MainTex, i.uv);
            	float inputDepth = tex2D (_DepthTex, i.uv);
            	float inputID = tex2D (_IDTex, i.uv);  
            	
            	if(inputDepth == 1 || inputID <= 0)
            	{            	
            		discard;
            	}             	
           	
            	float rimPower = 2.5;
				float rim = 1.0 - saturate(dot (normalize(float3(0.5,0,1)), inputNormal.xyz));  									
				fragColor = /*_Color2*/ + float4(0.8, 0.8, 0.75, 1) * pow (rim * 1, rimPower);
            		
          	}
          	ENDCG
		}
		
		Pass
		{
			ZTest On
			ZWrite On
			
			CGPROGRAM
			
			#pragma vertex vert_img
            #pragma fragment frag
            
			#include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D_float _DepthTex;	
			
//			static float IaoCap = 1.0f;
//			static float IaoMultiplier=10000.0f;
//			static float IdepthTolerance=0.00001;
//			static float IaoScale = 0.6;

			static float IaoCap = 0.9f;
			static float IaoMultiplier=10000.0f;
			static float IdepthTolerance=0.00001;
			static float IaoScale = 0.6;

			float readDepth( in float2 coord ) 
			{
				return LinearEyeDepth(tex2D (_DepthTex, coord).r) * 0.1;
			}

			float compareDepths( in float depth1, in float depth2 )
			{
				float ao=0.0;
				if (depth2>0.0 && depth1>0.0) 
				{
					float diff = sqrt( clamp( (depth1-depth2),0.0,1.0) );
									
					if (diff<0.15)
					ao = min(IaoCap,max(0.0,depth1-depth2-IdepthTolerance) * IaoMultiplier) * min(diff,0.1);
					
				}
				return ao;
			}				

			float4 frag (v2f_img i) : COLOR0
			{
				float3 color = tex2D(_MainTex, i.uv).rgb;
				float depth = readDepth(i.uv);				

				if(depth  == 1) discard;

//				return float4(depth, 0, 0, 1);
			
				float d;
				float pw = 5.0 / _ScreenParams.x;
				float ph = 5.0 / _ScreenParams.y;

				float aoCap = IaoCap;

				float ao = 0.0;			
				
				//float aoMultiplier=10000.0;
				float aoMultiplier= IaoMultiplier;
				float depthTolerance = IdepthTolerance;
				float aoscale= IaoScale;

				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;			    
			    
				d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;    
				
				pw*=2.0;
				ph*=2.0;
				aoMultiplier/=2.0;
				aoscale*=1.2;
				
				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
			    
				d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;    			    

				pw*=2.0;
				ph*=2.0;
				aoMultiplier/=2.0;
				aoscale*=1.2;
				
				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				
			  	d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale; 
			    
				pw*=2.0;
				ph*=2.0;
				aoMultiplier/=2.0;
				aoscale*=1.2;
				
				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
			    
			    d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;

				// ao/=4.0;
			    ao/=8.0;
			    ao = 1.0- (ao * 1.0);
//			    ao = 1.5*ao;

			    ao = clamp(ao, 0.0, 1.0 ) ;			    
//				return float4(float3(ao,ao,ao), 1.0);
				return float4(color * ao, 1.0);
			}
			
			
			ENDCG
		}			
						
	}
	Fallback Off
}	