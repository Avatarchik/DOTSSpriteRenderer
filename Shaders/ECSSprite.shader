Shader "Instanced/ECSSprite" {
    Properties {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    
    SubShader {
        Tags{
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }
        Cull Off
        Lighting Off
        ZWrite On
        Blend One OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            StructuredBuffer<float4x4> dataBuffer;

            struct v2f{
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
				half4  color    : COLOR0;
            };

            v2f vert (appdata_full v, uint instanceID : SV_InstanceID) { //TODO: dont use appdata_full
                float4x4 data = dataBuffer[instanceID];
                float3 flip = float3(data[3].xy, 1);
                float3 worldPos = data[0].xyz;
                float angle = data[0].w;
                float3 size = float3(data[2].xy, 1);
                float3 offset = float3(data[2].zw, 0);
                float scale = data[3].z;
                float3 pos = scale * (size * (v.vertex + float3(.5f, .5f, 0)) + offset);
                
                float s;
                float c;
                sincos(angle, s, c); 
                float4x4 mx = float4x4(  c, -s,  0,  0, 
                                         s,  c,  0,  0,
                                         0,  0,  1,  0,
                                         0,  0,  0,  1);
                pos = worldPos + mul(mx, pos) * flip;

                v2f o;
                o.pos = UnityObjectToClipPos(float4(pos, 1.0f));
                o.uv =  lerp(data[1].xy, data[1].zw, v.texcoord);
                o.color = fixed4(1, 1, 1, 1); 
                return o;
            }

            half4 frag (v2f i) : SV_Target{
                half4 col = tex2D(_MainTex, i.uv) * i.color;
				clip(col.a - 1.0 / 255.0);
                col.rgb *= col.a;
				return col;
            }

            ENDCG
        }
    }
}
